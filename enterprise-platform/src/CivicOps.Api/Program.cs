using System.Text;
using System.Threading.RateLimiting;
using CivicOps.Application.Behaviors;
using CivicOps.Application.Interfaces;
using CivicOps.Application.Validators;
using CivicOps.Infrastructure.BackgroundJobs;
using CivicOps.Infrastructure.Cache;
using CivicOps.Infrastructure.External.Firebase;
using CivicOps.Infrastructure.External.Gemini;
using CivicOps.Infrastructure.External.Twilio;
using CivicOps.Infrastructure.GPS;
using CivicOps.Infrastructure.Persistence;
using CivicOps.Infrastructure.Security;
using CivicOps.Api.Hubs;
using CivicOps.Api.Extensions;
using CivicOps.Api.Middleware;
using FluentValidation;
using Hangfire;
using Hangfire.PostgreSql;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Prometheus;
using Serilog;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// ── SERILOG ──────────────────────────────────────────────────────────────────
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Service", "CivicOps.Api")
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties}{NewLine}{Exception}")
    .WriteTo.File("logs/civicops-.log", rollingInterval: RollingInterval.Day, retainedFileCountLimit: 30)
    .CreateLogger();

builder.Host.UseSerilog();

var services = builder.Services;
var config = builder.Configuration;

// ── DATABASE ─────────────────────────────────────────────────────────────────
services.AddDbContext<CivicOpsDbContext>(opts =>
{
    opts.UseNpgsql(config.GetConnectionString("Default"), npg =>
    {
        npg.EnableRetryOnFailure(3, TimeSpan.FromSeconds(5), null);
        npg.CommandTimeout(30);
        npg.MigrationsHistoryTable("__ef_migrations_history");
    });
    if (builder.Environment.IsDevelopment())
        opts.EnableSensitiveDataLogging().EnableDetailedErrors();
});

// ── REDIS ─────────────────────────────────────────────────────────────────────
services.AddSingleton<IConnectionMultiplexer>(_ =>
    ConnectionMultiplexer.Connect(config.GetConnectionString("Redis") ?? "localhost:6379"));

// ── TENANT CONTEXT ───────────────────────────────────────────────────────────
services.AddHttpContextAccessor();
services.AddScoped<ITenantContext, HttpTenantContext>();
services.AddScoped<ICurrentUserContext, HttpCurrentUserContext>();

// ── REPOSITORIES & UOW ───────────────────────────────────────────────────────
services.AddScoped<CivicOps.Domain.Interfaces.IUnitOfWork, UnitOfWork>();

// ── CACHE SERVICES ───────────────────────────────────────────────────────────
services.AddSingleton<ICacheService, RedisCacheService>();
services.AddSingleton<ICivicOpsCacheService, RedisCacheService>();
services.AddSingleton<ILiveFleetCache, LiveFleetCache>();

// ── SECURITY SERVICES ────────────────────────────────────────────────────────
services.AddSingleton<ITokenService, JwtTokenService>();
services.AddScoped<IPasswordService, PasswordService>();
services.AddScoped<IMfaService, MfaService>();
services.AddScoped<IAuditService, AuditService>();

// ── AI PROVIDER ──────────────────────────────────────────────────────────────
services.AddHttpClient("gemini");
services.AddScoped<ILLMProvider, GeminiProvider>();

// ── NOTIFICATION SERVICES ────────────────────────────────────────────────────
services.AddSingleton<IPushNotificationService, FirebasePushNotificationService>();
services.AddScoped<ISmsService, TwilioSmsService>();
services.AddScoped<IEmailService, EmailService>();

// ── SIGNALR ──────────────────────────────────────────────────────────────────
services.AddSignalR(opts =>
{
    opts.EnableDetailedErrors = builder.Environment.IsDevelopment();
    opts.KeepAliveInterval = TimeSpan.FromSeconds(15);
    opts.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
    opts.MaximumReceiveMessageSize = 32 * 1024; // 32KB
})
.AddStackExchangeRedis(config.GetConnectionString("Redis") ?? "localhost:6379", opts =>
{
    opts.Configuration.ChannelPrefix = RedisChannel.Literal("civicops");
});

// SignalR broadcaster service
services.AddScoped<ISignalRService, SignalRNotificationService>();

// ── MEDIATR ──────────────────────────────────────────────────────────────────
services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(CivicOps.Application.Services.Auth.LoginCommandHandler).Assembly);
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(CachingBehavior<,>));
});

// ── FLUENTVALIDATION ─────────────────────────────────────────────────────────
services.AddValidatorsFromAssemblyContaining<LoginCommandValidator>();

// ── HANGFIRE ─────────────────────────────────────────────────────────────────
services.AddHangfire(cfg => cfg
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UsePostgreSqlStorage(c =>
        c.UseNpgsqlConnection(config.GetConnectionString("Default"))));

services.AddHangfireServer(opts =>
{
    opts.WorkerCount = 4;
    opts.Queues = new[] { "critical", "default", "maintenance" };
});

// Register background job classes
services.AddScoped<SlaMonitorJob>();
services.AddScoped<GeofenceCheckJob>();
services.AddScoped<MaintenanceAlertJob>();
services.AddScoped<AnalyticsRefreshJob>();

// ── JWT AUTHENTICATION ────────────────────────────────────────────────────────
var jwtKey = config["Jwt:Secret"] ?? throw new InvalidOperationException("JWT:Secret not configured");
services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opts =>
    {
        opts.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = config["Jwt:Issuer"],
            ValidAudience = config["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ClockSkew = TimeSpan.FromSeconds(30)
        };

        // Allow JWT in query string for SignalR WebSocket connections
        opts.Events = new JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                var token = ctx.Request.Query["access_token"];
                var path = ctx.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(token) && path.StartsWithSegments("/hubs"))
                    ctx.Token = token;
                return Task.CompletedTask;
            }
        };
    });

// ── AUTHORIZATION ─────────────────────────────────────────────────────────────
services.AddAuthorization(opts =>
{
    opts.AddPolicy("Dispatcher", p => p.RequireRole("SuperAdmin", "OperationsManager", "Dispatcher"));
    opts.AddPolicy("Supervisor", p => p.RequireRole("SuperAdmin", "OperationsManager", "Supervisor", "Dispatcher"));
    opts.AddPolicy("FleetManager", p => p.RequireRole("SuperAdmin", "OperationsManager", "FleetManager"));
    opts.AddPolicy("OperationsManager", p => p.RequireRole("SuperAdmin", "OperationsManager"));
    opts.AddPolicy("SuperAdmin", p => p.RequireRole("SuperAdmin"));
    opts.AddPolicy("AnyAuthenticated", p => p.RequireAuthenticatedUser());
});

// ── RATE LIMITING ─────────────────────────────────────────────────────────────
services.AddRateLimiter(opts =>
{
    // Global API rate limit
    opts.AddFixedWindowLimiter("api", limiter =>
    {
        limiter.PermitLimit = 300;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        limiter.QueueLimit = 10;
    });

    // Stricter limit for auth endpoints
    opts.AddFixedWindowLimiter("auth", limiter =>
    {
        limiter.PermitLimit = 10;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueLimit = 0;
    });

    // High-throughput GPS ingestion
    opts.AddFixedWindowLimiter("gps", limiter =>
    {
        limiter.PermitLimit = 2000;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueLimit = 100;
    });

    opts.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

// ── CORS ──────────────────────────────────────────────────────────────────────
services.AddCors(opts =>
{
    opts.AddPolicy("CivicOpsCors", policy =>
    {
        var origins = config.GetSection("Cors:AllowedOrigins").Get<string[]>()
            ?? new[] { "http://localhost:3000", "https://app.civicops.io" };

        policy.WithOrigins(origins)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials() // Required for SignalR
              .SetPreflightMaxAge(TimeSpan.FromHours(1));
    });
});

// ── CONTROLLERS ───────────────────────────────────────────────────────────────
services.AddControllers(opts =>
{
    opts.Filters.Add<GlobalExceptionFilter>();
}).AddJsonOptions(opts =>
{
    opts.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    opts.JsonSerializerOptions.DefaultIgnoreCondition =
        System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
});

// ── SWAGGER / OPENAPI ────────────────────────────────────────────────────────
services.AddEndpointsApiExplorer();
services.AddSwaggerGen(opts =>
{
    opts.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "CivicOps Command API",
        Version = "v1",
        Description = "Enterprise Operational Intelligence Platform — Fleet, Dispatch, Incidents, Analytics",
        Contact = new OpenApiContact { Name = "CivicOps", Email = "api@civicops.io" }
    });

    opts.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Enter your JWT access token"
    });

    opts.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

// ── HEALTH CHECKS ─────────────────────────────────────────────────────────────
services.AddHealthChecks()
    .AddNpgSql(config.GetConnectionString("Default")!, name: "postgres", tags: new[] { "db" })
    .AddRedis(config.GetConnectionString("Redis") ?? "localhost:6379", name: "redis", tags: new[] { "cache" });

// ── BUILD APP ─────────────────────────────────────────────────────────────────
var app = builder.Build();

// ── MIDDLEWARE PIPELINE ───────────────────────────────────────────────────────
app.UseSerilogRequestLogging(opts =>
{
    opts.MessageTemplate = "HTTP {RequestMethod} {RequestPath} → {StatusCode} in {Elapsed:0.0}ms";
    opts.EnrichDiagnosticContext = (ctx, httpCtx) =>
    {
        ctx.Set("TenantId", httpCtx.Items["TenantId"]);
        ctx.Set("UserId", httpCtx.User?.FindFirst("sub")?.Value);
    };
});

app.UseMetricServer();    // Prometheus metrics at /metrics
app.UseHttpMetrics();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "CivicOps Command API v1");
        c.RoutePrefix = "docs";
        c.DocExpansion(Swashbuckle.AspNetCore.SwaggerUI.DocExpansion.None);
    });
}

app.UseHttpsRedirection();
app.UseCors("CivicOpsCors");
app.UseRateLimiter();

// Custom middleware
app.UseMiddleware<TenantResolutionMiddleware>();
app.UseMiddleware<AuditLoggingMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

// ── ROUTES ────────────────────────────────────────────────────────────────────
app.MapControllers();

// SignalR hubs
app.MapHub<OperationsHub>("/hubs/operations").RequireAuthorization();
app.MapHub<FleetHub>("/hubs/fleet").RequireAuthorization();
app.MapHub<DispatchHub>("/hubs/dispatch").RequireAuthorization("Dispatcher");

// Health checks
app.MapHealthChecks("/health");
app.MapHealthChecks("/health/ready", new()
{
    Predicate = check => check.Tags.Contains("db")
});

// Hangfire dashboard (ops managers only in prod)
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = new[] { new HangfireAuthFilter() },
    DashboardTitle = "CivicOps Jobs"
});

// ── AUTO-MIGRATE & SEED ──────────────────────────────────────────────────────
await app.Services.InitializeDatabaseAsync(seed: app.Environment.IsDevelopment());

// ── REGISTER HANGFIRE JOBS ───────────────────────────────────────────────────
HangfireJobRegistration.RegisterRecurringJobs();

Log.Information("CivicOps Command API started on {Env}", app.Environment.EnvironmentName);

await app.RunAsync();
