using CivicOps.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CivicOps.Api.Extensions;

/// <summary>
/// Startup helpers for database initialization.
/// </summary>
public static class DatabaseInitializerExtensions
{
    public static async Task InitializeDatabaseAsync(this IServiceProvider services, bool seed)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CivicOpsDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<CivicOpsDbContext>>();

        try
        {
            logger.LogInformation("Applying database migrations...");
            await db.Database.MigrateAsync();

            // Apply TimescaleDB hypertable config (idempotent)
            await TimescaleSetup.ApplyAsync(db, logger);

            if (seed)
            {
                await CivicOps.Infrastructure.Persistence.Seed.SeedData
                    .SeedDemoTenantAsync(scope.ServiceProvider);
            }

            logger.LogInformation("Database initialization complete.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Database initialization failed.");
            throw;
        }
    }
}
