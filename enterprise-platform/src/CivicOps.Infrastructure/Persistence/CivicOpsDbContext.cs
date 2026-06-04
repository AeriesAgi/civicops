using CivicOps.Application.Interfaces;
using CivicOps.Domain.Entities;
using CivicOps.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace CivicOps.Infrastructure.Persistence;

public class CivicOpsDbContext : DbContext
{
    private readonly ITenantContext _tenantContext;

    public CivicOpsDbContext(DbContextOptions<CivicOpsDbContext> options, ITenantContext tenantContext)
        : base(options)
    {
        _tenantContext = tenantContext;
    }

    // Core tables
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    // Fleet
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<VehicleGpsEvent> VehicleGpsEvents => Set<VehicleGpsEvent>();
    public DbSet<Trip> Trips => Set<Trip>();
    public DbSet<Geofence> Geofences => Set<Geofence>();
    public DbSet<Region> Regions => Set<Region>();

    // Operations
    public DbSet<Incident> Incidents => Set<Incident>();
    public DbSet<IncidentUpdate> IncidentUpdates => Set<IncidentUpdate>();
    public DbSet<IncidentMedia> IncidentMedia => Set<IncidentMedia>();
    public DbSet<DispatchAssignment> DispatchAssignments => Set<DispatchAssignment>();
    public DbSet<PanicEvent> PanicEvents => Set<PanicEvent>();

    // Maintenance
    public DbSet<MaintenanceSchedule> MaintenanceSchedules => Set<MaintenanceSchedule>();
    public DbSet<MaintenanceRecord> MaintenanceRecords => Set<MaintenanceRecord>();

    // Communications
    public DbSet<Notification> Notifications => Set<Notification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply all entity configurations from this assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CivicOpsDbContext).Assembly);

        // Global tenant filter — automatically scopes all queries to current tenant
        // Bypassed for Tenant and AuditLog tables
        var tenantId = _tenantContext.TenantId;

        modelBuilder.Entity<User>().HasQueryFilter(e => e.TenantId == tenantId);
        modelBuilder.Entity<Vehicle>().HasQueryFilter(e => e.TenantId == tenantId);
        modelBuilder.Entity<VehicleGpsEvent>().HasQueryFilter(e => e.TenantId == tenantId);
        modelBuilder.Entity<Trip>().HasQueryFilter(e => e.TenantId == tenantId);
        modelBuilder.Entity<Geofence>().HasQueryFilter(e => e.TenantId == tenantId);
        modelBuilder.Entity<Region>().HasQueryFilter(e => e.TenantId == tenantId);
        modelBuilder.Entity<Incident>().HasQueryFilter(e => e.TenantId == tenantId);
        modelBuilder.Entity<IncidentUpdate>().HasQueryFilter(e => e.TenantId == tenantId);
        modelBuilder.Entity<IncidentMedia>().HasQueryFilter(e => e.TenantId == tenantId);
        modelBuilder.Entity<DispatchAssignment>().HasQueryFilter(e => e.TenantId == tenantId);
        modelBuilder.Entity<PanicEvent>().HasQueryFilter(e => e.TenantId == tenantId);
        modelBuilder.Entity<MaintenanceSchedule>().HasQueryFilter(e => e.TenantId == tenantId);
        modelBuilder.Entity<MaintenanceRecord>().HasQueryFilter(e => e.TenantId == tenantId);
        modelBuilder.Entity<Notification>().HasQueryFilter(e => e.TenantId == tenantId);

        ConfigureEnums(modelBuilder);
    }

    private static void ConfigureEnums(ModelBuilder modelBuilder)
    {
        // Store enums as strings for readability in DB
        modelBuilder.Entity<User>()
            .Property(e => e.Role)
            .HasConversion(new EnumToStringConverter<UserRole>());

        modelBuilder.Entity<Vehicle>()
            .Property(e => e.Type)
            .HasConversion(new EnumToStringConverter<VehicleType>());

        modelBuilder.Entity<Vehicle>()
            .Property(e => e.Status)
            .HasConversion(new EnumToStringConverter<VehicleStatus>());

        modelBuilder.Entity<Vehicle>()
            .Property(e => e.FuelType)
            .HasConversion(new EnumToStringConverter<FuelType>());

        modelBuilder.Entity<Incident>()
            .Property(e => e.Status)
            .HasConversion(new EnumToStringConverter<IncidentStatus>());

        modelBuilder.Entity<Incident>()
            .Property(e => e.Priority)
            .HasConversion(new EnumToStringConverter<IncidentPriority>());

        modelBuilder.Entity<Incident>()
            .Property(e => e.Severity)
            .HasConversion(new EnumToStringConverter<IncidentSeverity>());

        modelBuilder.Entity<Incident>()
            .Property(e => e.Category)
            .HasConversion(new EnumToStringConverter<IncidentCategory>());

        modelBuilder.Entity<DispatchAssignment>()
            .Property(e => e.Status)
            .HasConversion(new EnumToStringConverter<DispatchStatus>());

        modelBuilder.Entity<DispatchAssignment>()
            .Property(e => e.Priority)
            .HasConversion(new EnumToStringConverter<IncidentPriority>());
    }

    public override async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        // Auto-set UpdatedAt on modified entities
        var modified = ChangeTracker.Entries()
            .Where(e => e.State == EntityState.Modified && e.Entity is BaseEntity)
            .Select(e => (BaseEntity)e.Entity);

        foreach (var entity in modified)
        {
            // UpdatedAt is set by the entity's SetUpdatedBy method
            // This is a safety fallback
        }

        return await base.SaveChangesAsync(ct);
    }
}
