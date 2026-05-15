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

var app = builder.Build();

// Initialize data service
var dataService = app.Services.GetRequiredService<IDataService>();
await dataService.InitializeAsync();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();