using CivicOps.Application.Interfaces;
using CivicOps.Domain.Interfaces;
using Hangfire.Dashboard;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Net;
using System.Security.Claims;
using System.Text.Json;

namespace CivicOps.Api.Middleware;

/// <summary>
/// Resolves the current tenant from subdomain, custom domain, or JWT claim.
/// Sets TenantId in HttpContext.Items for downstream use.
/// </summary>
public class TenantResolutionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<TenantResolutionMiddleware> _logger;

    public TenantResolutionMiddleware(RequestDelegate next, ILogger<TenantResolutionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, ITenantRepository tenantRepo)
    {
        // Skip tenant resolution for public endpoints
        var path = context.Request.Path.Value ?? "";
        if (path.StartsWith("/health") || path.StartsWith("/metrics") || path.StartsWith("/hangfire"))
        {
            await _next(context);
            return;
        }

        // 1. Try JWT claim first (most reliable for authenticated requests)
        if (context.User?.Identity?.IsAuthenticated == true)
        {
            var tenantIdClaim = context.User.FindFirstValue("tenant_id");
            if (Guid.TryParse(tenantIdClaim, out var tenantIdFromJwt))
            {
                context.Items["TenantId"] = tenantIdFromJwt;
                await _next(context);
                return;
            }
        }

        // 2. Try subdomain resolution (e.g. acme.civicops.io → slug "acme")
        var host = context.Request.Host.Host;
        var parts = host.Split('.');

        if (parts.Length >= 3)
        {
            var slug = parts[0].ToLowerInvariant();
            if (slug != "www" && slug != "api" && slug != "app")
            {
                var tenant = await tenantRepo.GetBySlugAsync(slug);
                if (tenant is not null && tenant.IsActive)
                {
                    context.Items["TenantId"] = tenant.Id;
                    context.Items["TenantSlug"] = tenant.Slug;
                    await _next(context);
                    return;
                }
            }
        }

        // 3. Try X-Tenant-Slug header (API integrations)
        var tenantSlugHeader = context.Request.Headers["X-Tenant-Slug"].FirstOrDefault();
        if (!string.IsNullOrEmpty(tenantSlugHeader))
        {
            var tenant = await tenantRepo.GetBySlugAsync(tenantSlugHeader);
            if (tenant is not null && tenant.IsActive)
            {
                context.Items["TenantId"] = tenant.Id;
                context.Items["TenantSlug"] = tenant.Slug;
                await _next(context);
                return;
            }
        }

        // 4. For development: fall through without tenant (allows Swagger testing)
        if (context.RequestServices.GetRequiredService<IWebHostEnvironment>().IsDevelopment())
        {
            await _next(context);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsJsonAsync(new { error = "Tenant could not be resolved." });
    }
}

/// <summary>
/// Logs all mutating requests (POST/PUT/PATCH/DELETE) to the audit log.
/// </summary>
public class AuditLoggingMiddleware
{
    private readonly RequestDelegate _next;

    public AuditLoggingMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, IAuditService auditService)
    {
        await _next(context);

        var method = context.Request.Method;
        if (method is "POST" or "PUT" or "PATCH" or "DELETE"
            && context.User?.Identity?.IsAuthenticated == true
            && context.Response.StatusCode < 400)
        {
            var userId = Guid.TryParse(context.User.FindFirstValue("sub"), out var uid)
                ? uid : (Guid?)null;
            var tenantId = context.Items.TryGetValue("TenantId", out var tid) && tid is Guid tguid
                ? tguid : Guid.Empty;

            if (tenantId != Guid.Empty)
            {
                await auditService.LogAsync(
                    tenantId, userId,
                    $"{method} {context.Request.Path}",
                    ipAddress: context.Connection.RemoteIpAddress?.ToString()
                );
            }
        }
    }
}

/// <summary>
/// Global exception handler — returns consistent error responses.
/// </summary>
public class GlobalExceptionFilter : IExceptionFilter
{
    private readonly ILogger<GlobalExceptionFilter> _logger;

    public GlobalExceptionFilter(ILogger<GlobalExceptionFilter> logger) => _logger = logger;

    public void OnException(ExceptionContext context)
    {
        _logger.LogError(context.Exception, "Unhandled exception in {Path}",
            context.HttpContext.Request.Path);

        var (status, message) = context.Exception switch
        {
            UnauthorizedAccessException => (HttpStatusCode.Unauthorized, "Unauthorized."),
            KeyNotFoundException => (HttpStatusCode.NotFound, "Resource not found."),
            ArgumentException ex => (HttpStatusCode.BadRequest, ex.Message),
            InvalidOperationException ex => (HttpStatusCode.BadRequest, ex.Message),
            FluentValidation.ValidationException ex =>
                (HttpStatusCode.UnprocessableEntity, string.Join("; ", ex.Errors.Select(e => e.ErrorMessage))),
            _ => (HttpStatusCode.InternalServerError, "An unexpected error occurred.")
        };

        context.Result = new ObjectResult(new
        {
            success = false,
            message,
            traceId = context.HttpContext.TraceIdentifier
        })
        { StatusCode = (int)status };

        context.ExceptionHandled = true;
    }
}

/// <summary>
/// HTTP-scoped tenant context — reads from HttpContext.Items set by middleware.
/// </summary>
public class HttpTenantContext : ITenantContext
{
    private readonly IHttpContextAccessor _accessor;

    public HttpTenantContext(IHttpContextAccessor accessor) => _accessor = accessor;

    public Guid TenantId
    {
        get
        {
            var ctx = _accessor.HttpContext;
            if (ctx?.Items.TryGetValue("TenantId", out var tid) == true && tid is Guid g)
                return g;

            // Fallback to JWT claim
            var claim = ctx?.User?.FindFirstValue("tenant_id");
            return Guid.TryParse(claim, out var fromJwt) ? fromJwt : Guid.Empty;
        }
    }

    public string TenantSlug
    {
        get
        {
            var ctx = _accessor.HttpContext;
            return ctx?.Items.TryGetValue("TenantSlug", out var slug) == true
                ? slug?.ToString() ?? string.Empty
                : ctx?.User?.FindFirstValue("tenant_slug") ?? string.Empty;
        }
    }

    public void SetTenant(string tenantId)
    {
        if (_accessor.HttpContext is not null)
            _accessor.HttpContext.Items["TenantId"] = Guid.Parse(tenantId);
    }
}

/// <summary>
/// HTTP-scoped current user context — reads JWT claims.
/// </summary>
public class HttpCurrentUserContext : ICurrentUserContext
{
    private readonly IHttpContextAccessor _accessor;

    public HttpCurrentUserContext(IHttpContextAccessor accessor) => _accessor = accessor;

    private ClaimsPrincipal? User => _accessor.HttpContext?.User;

    public Guid UserId => Guid.TryParse(User?.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User?.FindFirstValue("sub"), out var id) ? id : Guid.Empty;

    public Guid TenantId => Guid.TryParse(User?.FindFirstValue("tenant_id"), out var id) ? id : Guid.Empty;

    public string Email => User?.FindFirstValue(ClaimTypes.Email)
        ?? User?.FindFirstValue("email") ?? string.Empty;

    public string Role => User?.FindFirstValue(ClaimTypes.Role)
        ?? User?.FindFirstValue("role") ?? string.Empty;

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated == true;
}

/// <summary>
/// Hangfire dashboard authorization — restricts to OperationsManager+ in production.
/// </summary>
public class HangfireAuthFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var http = context.GetHttpContext();
        if (http.RequestServices.GetRequiredService<IWebHostEnvironment>().IsDevelopment())
            return true;

        return http.User?.IsInRole("SuperAdmin") == true
            || http.User?.IsInRole("OperationsManager") == true;
    }
}
