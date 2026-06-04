using CivicOps.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CivicOps.Infrastructure.Persistence.Configurations;

public class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> b)
    {
        b.ToTable("tenants");
        b.HasKey(e => e.Id);
        b.Property(e => e.Name).HasMaxLength(200).IsRequired();
        b.Property(e => e.Slug).HasMaxLength(100).IsRequired();
        b.HasIndex(e => e.Slug).IsUnique();
        b.HasIndex(e => e.CustomDomain).IsUnique().HasFilter("custom_domain IS NOT NULL");
        b.Property(e => e.Tier).HasMaxLength(30).HasDefaultValue("professional");
        b.Property(e => e.LogoUrl).HasMaxLength(500);
        b.Property(e => e.PrimaryColor).HasMaxLength(10);
        b.Property(e => e.SecondaryColor).HasMaxLength(10);
        b.Property(e => e.CustomDomain).HasMaxLength(200);
        b.Property(e => e.SupportEmail).HasMaxLength(320);
        b.Property(e => e.TimeZone).HasMaxLength(50).HasDefaultValue("Africa/Johannesburg");
        b.Property(e => e.CreatedAt).HasColumnType("timestamptz");
        b.Property(e => e.UpdatedAt).HasColumnType("timestamptz");
        b.Property(e => e.ExpiresAt).HasColumnType("timestamptz");
    }
}

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> b)
    {
        b.ToTable("users");
        b.HasKey(e => e.Id);
        b.Property(e => e.Email).HasMaxLength(320).IsRequired();
        b.HasIndex(e => e.Email).IsUnique();
        b.Property(e => e.PasswordHash).HasMaxLength(256).IsRequired();
        b.Property(e => e.FullName).HasMaxLength(200).IsRequired();
        b.Property(e => e.EmployeeId).HasMaxLength(50);
        b.Property(e => e.Phone).HasMaxLength(20);
        b.Property(e => e.MfaSecret).HasMaxLength(100);
        b.Property(e => e.FcmToken).HasMaxLength(512);
        b.Property(e => e.ProfileImageUrl).HasMaxLength(500);
        b.Property(e => e.CreatedAt).HasColumnType("timestamptz");
        b.Property(e => e.UpdatedAt).HasColumnType("timestamptz");
        b.Property(e => e.LastLoginAt).HasColumnType("timestamptz");
        b.Property(e => e.LockedUntil).HasColumnType("timestamptz");

        b.HasOne(e => e.Tenant).WithMany(t => t.Users).HasForeignKey(e => e.TenantId);
        b.HasOne(e => e.Region).WithMany(r => r.Users).HasForeignKey(e => e.RegionId);
        b.HasMany(e => e.RefreshTokens).WithOne(r => r.User).HasForeignKey(r => r.UserId).OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(e => new { e.TenantId, e.Email });
        b.HasIndex(e => e.TenantId);
    }
}

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> b)
    {
        b.ToTable("refresh_tokens");
        b.HasKey(e => e.Id);
        b.Property(e => e.Token).HasMaxLength(512).IsRequired();
        b.HasIndex(e => e.Token).IsUnique();
        b.Property(e => e.DeviceId).HasMaxLength(200);
        b.Property(e => e.IpAddress).HasMaxLength(45);
        b.Property(e => e.UserAgent).HasMaxLength(1000);
        b.Property(e => e.ReplacedByToken).HasMaxLength(512);
        b.Property(e => e.ExpiresAt).HasColumnType("timestamptz");
        b.Property(e => e.RevokedAt).HasColumnType("timestamptz");
        b.Property(e => e.CreatedAt).HasColumnType("timestamptz");
    }
}

public class VehicleConfiguration : IEntityTypeConfiguration<Vehicle>
{
    public void Configure(EntityTypeBuilder<Vehicle> b)
    {
        b.ToTable("vehicles");
        b.HasKey(e => e.Id);
        b.Property(e => e.Registration).HasMaxLength(20).IsRequired();
        b.HasIndex(e => new { e.TenantId, e.Registration }).IsUnique();
        b.Property(e => e.Alias).HasMaxLength(50);
        b.Property(e => e.Make).HasMaxLength(100);
        b.Property(e => e.Model).HasMaxLength(100);
        b.Property(e => e.Color).HasMaxLength(50);
        b.Property(e => e.Vin).HasMaxLength(17);
        b.Property(e => e.GpsDeviceId).HasMaxLength(100);
        b.Property(e => e.TrackerProvider).HasMaxLength(50);
        b.Property(e => e.OdometerKm).HasPrecision(10, 2);
        b.Property(e => e.FuelCapacityL).HasPrecision(8, 2);
        b.Property(e => e.LastLatitude).HasPrecision(10, 7);
        b.Property(e => e.LastLongitude).HasPrecision(10, 7);
        b.Property(e => e.LastSpeedKmh).HasPrecision(6, 2);
        b.Property(e => e.LastHeadingDeg).HasPrecision(5, 2);
        b.Property(e => e.LastFuelLevelPct).HasPrecision(5, 2);
        b.Property(e => e.CreatedAt).HasColumnType("timestamptz");
        b.Property(e => e.UpdatedAt).HasColumnType("timestamptz");
        b.Property(e => e.LastGpsAt).HasColumnType("timestamptz");

        b.HasOne(e => e.Tenant).WithMany(t => t.Vehicles).HasForeignKey(e => e.TenantId);
        b.HasOne(e => e.AssignedDriver).WithMany().HasForeignKey(e => e.AssignedDriverId);
        b.HasMany(e => e.GpsEvents).WithOne(g => g.Vehicle).HasForeignKey(g => g.VehicleId);
        b.HasMany(e => e.MaintenanceSchedules).WithOne(m => m.Vehicle).HasForeignKey(m => m.VehicleId);

        b.HasIndex(e => e.TenantId);
        b.HasIndex(e => e.GpsDeviceId);
        b.HasIndex(e => new { e.TenantId, e.Status });
    }
}

public class VehicleGpsEventConfiguration : IEntityTypeConfiguration<VehicleGpsEvent>
{
    public void Configure(EntityTypeBuilder<VehicleGpsEvent> b)
    {
        b.ToTable("vehicle_gps_events");
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).UseIdentityByDefaultColumn();
        b.Property(e => e.Latitude).HasPrecision(10, 7).IsRequired();
        b.Property(e => e.Longitude).HasPrecision(10, 7).IsRequired();
        b.Property(e => e.SpeedKmh).HasPrecision(6, 2);
        b.Property(e => e.HeadingDeg).HasPrecision(5, 2);
        b.Property(e => e.AccuracyM).HasPrecision(6, 2);
        b.Property(e => e.AltitudeM).HasPrecision(8, 2);
        b.Property(e => e.FuelLevelPct).HasPrecision(5, 2);
        b.Property(e => e.OdometerKm).HasPrecision(10, 2);
        b.Property(e => e.EventType).HasMaxLength(30).HasDefaultValue("position");
        b.Property(e => e.RecordedAt).HasColumnType("timestamptz");
        b.Property(e => e.ReceivedAt).HasColumnType("timestamptz");

        b.HasIndex(e => new { e.VehicleId, e.RecordedAt });
        b.HasIndex(e => new { e.TenantId, e.RecordedAt });
    }
}

public class IncidentConfiguration : IEntityTypeConfiguration<Incident>
{
    public void Configure(EntityTypeBuilder<Incident> b)
    {
        b.ToTable("incidents");
        b.HasKey(e => e.Id);
        b.Property(e => e.ReferenceNo).HasMaxLength(30).IsRequired();
        b.HasIndex(e => e.ReferenceNo).IsUnique();
        b.Property(e => e.Title).HasMaxLength(300).IsRequired();
        b.Property(e => e.Description).HasColumnType("text");
        b.Property(e => e.SubCategory).HasMaxLength(50);
        b.Property(e => e.Address).HasColumnType("text");
        b.Property(e => e.Latitude).HasPrecision(10, 7);
        b.Property(e => e.Longitude).HasPrecision(10, 7);
        b.Property(e => e.AiSummary).HasColumnType("text");
        b.Property(e => e.AiPriorityScore).HasPrecision(5, 4);
        b.Property(e => e.Tags).HasColumnType("text[]");
        b.Property(e => e.OpenedAt).HasColumnType("timestamptz");
        b.Property(e => e.CreatedAt).HasColumnType("timestamptz");
        b.Property(e => e.UpdatedAt).HasColumnType("timestamptz");
        b.Property(e => e.FirstResponseAt).HasColumnType("timestamptz");
        b.Property(e => e.ResolvedAt).HasColumnType("timestamptz");
        b.Property(e => e.ClosedAt).HasColumnType("timestamptz");

        b.Ignore(e => e.DomainEvents);

        b.HasOne(e => e.Tenant).WithMany(t => t.Incidents).HasForeignKey(e => e.TenantId);
        b.HasOne(e => e.ReportedBy).WithMany().HasForeignKey(e => e.ReportedById);
        b.HasOne(e => e.AssignedTo).WithMany().HasForeignKey(e => e.AssignedToId);
        b.HasOne(e => e.AssignedVehicle).WithMany().HasForeignKey(e => e.AssignedVehicleId);
        b.HasOne(e => e.EscalatedTo).WithMany().HasForeignKey(e => e.EscalatedToId);
        b.HasMany(e => e.Updates).WithOne(u => u.Incident).HasForeignKey(u => u.IncidentId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(e => e.Media).WithOne(m => m.Incident).HasForeignKey(m => m.IncidentId).OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(e => new { e.TenantId, e.Status });
        b.HasIndex(e => new { e.TenantId, e.OpenedAt });
        b.HasIndex(e => new { e.TenantId, e.Priority, e.Status });
    }
}

public class DispatchAssignmentConfiguration : IEntityTypeConfiguration<DispatchAssignment>
{
    public void Configure(EntityTypeBuilder<DispatchAssignment> b)
    {
        b.ToTable("dispatch_assignments");
        b.HasKey(e => e.Id);
        b.Property(e => e.AiConfidence).HasPrecision(5, 4);
        b.Property(e => e.AiReasoning).HasColumnType("text");
        b.Property(e => e.OptimizedRouteJson).HasColumnType("jsonb");
        b.Property(e => e.Notes).HasColumnType("text");
        b.Property(e => e.EstDistanceKm).HasPrecision(8, 2);
        b.Property(e => e.OriginLat).HasPrecision(10, 7);
        b.Property(e => e.OriginLng).HasPrecision(10, 7);
        b.Property(e => e.DestLat).HasPrecision(10, 7);
        b.Property(e => e.DestLng).HasPrecision(10, 7);
        b.Property(e => e.DispatchedAt).HasColumnType("timestamptz");
        b.Property(e => e.AcknowledgedAt).HasColumnType("timestamptz");
        b.Property(e => e.ArrivedAt).HasColumnType("timestamptz");
        b.Property(e => e.CompletedAt).HasColumnType("timestamptz");
        b.Property(e => e.CreatedAt).HasColumnType("timestamptz");

        b.HasOne(e => e.Vehicle).WithMany(v => v.DispatchAssignments).HasForeignKey(e => e.VehicleId);
        b.HasOne(e => e.Driver).WithMany(u => u.Dispatches).HasForeignKey(e => e.DriverId);
        b.HasOne(e => e.Incident).WithMany(i => i.Assignments).HasForeignKey(e => e.IncidentId);

        b.HasIndex(e => new { e.TenantId, e.Status });
        b.HasIndex(e => e.VehicleId);
    }
}

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> b)
    {
        b.ToTable("audit_logs");
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).UseIdentityByDefaultColumn();
        b.Property(e => e.Action).HasMaxLength(100).IsRequired();
        b.Property(e => e.EntityType).HasMaxLength(100);
        b.Property(e => e.EntityId).HasMaxLength(200);
        b.Property(e => e.IpAddress).HasMaxLength(45);
        b.Property(e => e.OldValuesJson).HasColumnType("jsonb");
        b.Property(e => e.NewValuesJson).HasColumnType("jsonb");
        b.Property(e => e.CreatedAt).HasColumnType("timestamptz");
        b.HasIndex(e => new { e.TenantId, e.CreatedAt });
        b.HasIndex(e => new { e.TenantId, e.EntityType, e.EntityId });
    }
}
