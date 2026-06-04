using CivicOps.Services;
using CivicOps.Band;
using CivicOps.Band.Agents;
using CivicOps.Hubs;
using Microsoft.AspNetCore.StaticFiles;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddHttpClient();
builder.Services.AddSignalR();

// Register CivicOps services
builder.Services.AddSingleton<IDataService, JsonDataService>();
builder.Services.AddSingleton<DeterministicClassificationService>();
builder.Services.AddSingleton<IClassificationService>(sp => 
    sp.GetRequiredService<DeterministicClassificationService>());
builder.Services.AddSingleton<IGeminiService, GeminiService>();
builder.Services.AddSingleton<IDemoAuthService, DemoAuthService>();
builder.Services.AddSingleton<IResidentAuthService, ResidentAuthService>();
builder.Services.AddSingleton<IWeatherService, WeatherService>();
builder.Services.AddSingleton<IIncidentIntakeService, IncidentIntakeService>();
builder.Services.AddSingleton<IWhatsAppService, WhatsAppService>();

// ── Band multi-agent coordination layer ───────────────────────────────────
var bandOptions = new BandOptions();
builder.Configuration.GetSection("Band").Bind(bandOptions);
builder.Services.AddSingleton(bandOptions);

// The Band interaction layer (one shared instance behind the transport seam).
builder.Services.AddSingleton<LocalBandBroker>();
builder.Services.AddSingleton<IBandTransport>(sp => sp.GetRequiredService<LocalBandBroker>());

// Command fleet the DispatchCoordinatorAgent matches against.
builder.Services.AddSingleton<IFleetService, InMemoryFleetService>();

// The three Band-resident agents (singletons → connected for the app lifetime).
builder.Services.AddSingleton<IncidentIntakeAgent>();
builder.Services.AddSingleton<DispatchCoordinatorAgent>();
builder.Services.AddSingleton<ResponseMonitorAgent>();

// Facade, realtime bridge, optional live mirror and simulation driver.
builder.Services.AddSingleton<BandAgentService>();
builder.Services.AddSingleton<BandRealtimeBroadcaster>();
builder.Services.AddSingleton<BandHttpGateway>();
builder.Services.AddSingleton<BandSimulationService>();

// Add session support for demo authentication
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(8);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.Name = ".CivicOps.Session";
});

var app = builder.Build();

// Initialize services
var dataService = app.Services.GetRequiredService<IDataService>();
await dataService.InitializeAsync();

var authService = app.Services.GetRequiredService<IDemoAuthService>();
await authService.InitializeAsync();

var residentAuthService = app.Services.GetRequiredService<IResidentAuthService>();
await residentAuthService.InitializeAsync();

// Bring the Band coordination layer online: constructing these wires the three
// agents, the SignalR bridge and (if configured) the live band.ai mirror onto
// the shared interaction layer before any traffic arrives.
_ = app.Services.GetRequiredService<BandAgentService>();
_ = app.Services.GetRequiredService<BandRealtimeBroadcaster>();
_ = app.Services.GetRequiredService<BandHttpGateway>();
_ = app.Services.GetRequiredService<BandSimulationService>();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

var contentTypeProvider = new FileExtensionContentTypeProvider();
contentTypeProvider.Mappings[".apk"] = "application/vnd.android.package-archive";
app.UseStaticFiles(new StaticFileOptions
{
    ContentTypeProvider = contentTypeProvider
});

app.UseRouting();

app.UseSession();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Real-time stream for the Band Room Viewer.
app.MapHub<BandHub>("/hubs/band");

app.Run();