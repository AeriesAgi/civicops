using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CivicOps.Infrastructure.Persistence;

/// <summary>
/// Applies TimescaleDB-specific configuration that EF Core migrations cannot express:
///   - Converts vehicle_gps_events into a hypertable
///   - Adds a 90-day retention policy
///   - Creates the hourly continuous aggregate
/// Called once after migrations during startup.
/// Safe to run repeatedly (all operations are idempotent).
/// </summary>
public static class TimescaleSetup
{
    public static async Task ApplyAsync(CivicOpsDbContext db, ILogger logger)
    {
        try
        {
            // Convert GPS events table to a hypertable (partitioned by time)
            await db.Database.ExecuteSqlRawAsync("""
                SELECT create_hypertable('vehicle_gps_events', 'recorded_at',
                    chunk_time_interval => INTERVAL '1 day',
                    if_not_exists => TRUE,
                    migrate_data => TRUE);
                """);

            // 90-day retention for raw GPS pings
            await db.Database.ExecuteSqlRawAsync("""
                SELECT add_retention_policy('vehicle_gps_events',
                    INTERVAL '90 days', if_not_exists => TRUE);
                """);

            // Hourly continuous aggregate for fast analytics
            await db.Database.ExecuteSqlRawAsync("""
                CREATE MATERIALIZED VIEW IF NOT EXISTS gps_hourly_stats
                WITH (timescaledb.continuous) AS
                SELECT
                    time_bucket('1 hour', recorded_at) AS bucket,
                    vehicle_id,
                    tenant_id,
                    AVG(speed_kmh) AS avg_speed,
                    MAX(speed_kmh) AS max_speed,
                    COUNT(*) AS ping_count
                FROM vehicle_gps_events
                GROUP BY bucket, vehicle_id, tenant_id
                WITH NO DATA;
                """);

            logger.LogInformation("TimescaleDB hypertable configuration applied.");
        }
        catch (Exception ex)
        {
            // Non-fatal — system works without hypertable, just less optimised
            logger.LogWarning(ex, "TimescaleDB setup skipped (extension may be unavailable).");
        }
    }
}
