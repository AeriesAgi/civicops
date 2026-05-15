using CivicOps.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddHttpClient();

// Register CivicOps services
builder.Services.AddSingleton<IDataService, JsonDataService>();
builder.Services.AddSingleton<DeterministicClassificationService>();
builder.Services.AddSingleton<IClassificationService>(sp => 
    sp.GetRequiredService<DeterministicClassificationService>());
builder.Services.AddSingleton<IGeminiService, GeminiService>();
builder.Services.AddSingleton<IDemoAuthService, DemoAuthService>();

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

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseSession();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();