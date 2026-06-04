using CivicOps.Domain.Enums;

namespace CivicOps.Domain.Entities;

public class Region : TenantEntity
{
    public string Name { get; private set; } = string.Empty;
    public string? Code { get; private set; }
    public bool IsActive { get; private set; } = true;

    public ICollection<User> Users { get; private set; } = new List<User>();
    public ICollection<Vehicle> Vehicles { get; private set; } = new List<Vehicle>();
    public ICollection<Incident> Incidents { get; private set; } = new List<Incident>();

    private Region() { }

    public static Region Create(Guid tenantId, string name, string? code = null)
    {
        return new Region
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = name,
            Code = code,
            CreatedAt = DateTime.UtcNow
        };
    }
}

public class Trip : TenantEntity
{
    public Guid VehicleId { get; private set; }
    public Guid? DriverId { get; private set; }
    public Guid? AssignmentId { get; private set; }
    public decimal? StartLat { get; private set; }
    public decimal? StartLng { get; private set; }
    public decimal? EndLat { get; private set; }
    public decimal? EndLng { get; private set; }
    public decimal? DistanceKm { get; private set; }
    public int? DurationMinutes { get; private set; }
    public decimal? MaxSpeedKmh { get; private set; }
    public decimal? AvgSpeedKmh { get; private set; }
    public int IdleMinutes { get; private set; } = 0;
    public decimal? FuelUsedL { get; private set; }
    public DateTime StartedAt { get; private set; }
    public DateTime? EndedAt { get; private set; }
    public string Status { get; private set; } = "active";

    public Vehicle? Vehicle { get; private set; }
    public User? Driver { get; private set; }

    private Trip() { }

    public static Trip Start(Guid tenantId, Guid vehicleId, Guid? driverId,
        decimal startLat, decimal startLng)
    {
        return new Trip
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            VehicleId = vehicleId,
            DriverId = driverId,
            StartLat = startLat,
            StartLng = startLng,
            StartedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void End(decimal endLat, decimal endLng, decimal distanceKm,
        decimal? maxSpeed, decimal? avgSpeed, decimal? fuelUsed, int idleMinutes)
    {
        EndLat = endLat;
        EndLng = endLng;
        DistanceKm = distanceKm;
        MaxSpeedKmh = maxSpeed;
        AvgSpeedKmh = avgSpeed;
        FuelUsedL = fuelUsed;
        IdleMinutes = idleMinutes;
        EndedAt = DateTime.UtcNow;
        DurationMinutes = (int)(DateTime.UtcNow - StartedAt).TotalMinutes;
        Status = "completed";
        UpdatedAt = DateTime.UtcNow;
    }
}

public class Geofence : TenantEntity
{
    public string Name { get; private set; } = string.Empty;
    public string Type { get; private set; } = "circle";
    public decimal? CenterLat { get; private set; }
    public decimal? CenterLng { get; private set; }
    public decimal? RadiusM { get; private set; }
    public string? PolygonCoordinatesJson { get; private set; }
    public bool AlertOnEnter { get; private set; } = true;
    public bool AlertOnExit { get; private set; } = true;
    public string AppliesToJson { get; private set; } = "{\"all\": true}";
    public bool IsActive { get; private set; } = true;

    private Geofence() { }

    public static Geofence CreateCircle(Guid tenantId, string name,
        decimal centerLat, decimal centerLng, decimal radiusM)
    {
        return new Geofence
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = name,
            Type = "circle",
            CenterLat = centerLat,
            CenterLng = centerLng,
            RadiusM = radiusM,
            CreatedAt = DateTime.UtcNow
        };
    }

    public bool ContainsPoint(decimal lat, decimal lng)
    {
        if (Type != "circle" || !CenterLat.HasValue || !CenterLng.HasValue || !RadiusM.HasValue)
            return false;

        var dist = GeoCalculator.HaversineMeters(
            (double)CenterLat.Value, (double)CenterLng.Value,
            (double)lat, (double)lng);

        return dist <= (double)RadiusM.Value;
    }
}

public class MaintenanceSchedule : TenantEntity
{
    public Guid VehicleId { get; private set; }
    public string Type { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public int? IntervalKm { get; private set; }
    public int? IntervalDays { get; private set; }
    public decimal? LastServiceKm { get; private set; }
    public DateTime? LastServiceDate { get; private set; }
    public decimal? NextDueKm { get; private set; }
    public DateTime? NextDueDate { get; private set; }
    public string Priority { get; private set; } = "routine";
    public bool AiPredicted { get; private set; } = false;
    public decimal? AiConfidence { get; private set; }
    public decimal? EstimatedCost { get; private set; }
    public bool IsActive { get; private set; } = true;

    public Vehicle? Vehicle { get; private set; }

    private MaintenanceSchedule() { }

    public static MaintenanceSchedule Create(Guid tenantId, Guid vehicleId,
        string type, string? description = null,
        int? intervalKm = null, int? intervalDays = null,
        decimal? estimatedCost = null)
    {
        return new MaintenanceSchedule
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            VehicleId = vehicleId,
            Type = type,
            Description = description,
            IntervalKm = intervalKm,
            IntervalDays = intervalDays,
            EstimatedCost = estimatedCost,
            CreatedAt = DateTime.UtcNow
        };
    }

    public bool IsDue(decimal currentOdometer)
    {
        if (NextDueKm.HasValue && currentOdometer >= NextDueKm.Value) return true;
        if (NextDueDate.HasValue && DateTime.UtcNow >= NextDueDate.Value) return true;
        return false;
    }
}

public class MaintenanceRecord : TenantEntity
{
    public Guid VehicleId { get; private set; }
    public Guid? ScheduleId { get; private set; }
    public string Type { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public string? Technician { get; private set; }
    public string? Workshop { get; private set; }
    public decimal? Cost { get; private set; }
    public decimal? OdometerAtService { get; private set; }
    public string PartsUsedJson { get; private set; } = "[]";
    public List<string> Documents { get; private set; } = new();
    public DateTime ServicedAt { get; private set; }
    public DateTime? NextDueDate { get; private set; }
    public decimal? NextDueKm { get; private set; }

    public Vehicle? Vehicle { get; private set; }
    public MaintenanceSchedule? Schedule { get; private set; }

    private MaintenanceRecord() { }

    public static MaintenanceRecord Create(Guid tenantId, Guid vehicleId,
        string type, string description, DateTime servicedAt,
        Guid? scheduleId = null, string? technician = null,
        string? workshop = null, decimal? cost = null,
        decimal? odometerAtService = null)
    {
        return new MaintenanceRecord
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            VehicleId = vehicleId,
            ScheduleId = scheduleId,
            Type = type,
            Description = description,
            Technician = technician,
            Workshop = workshop,
            Cost = cost,
            OdometerAtService = odometerAtService,
            ServicedAt = servicedAt,
            CreatedAt = DateTime.UtcNow
        };
    }
}

public class PanicEvent : TenantEntity
{
    public Guid UserId { get; private set; }
    public Guid? VehicleId { get; private set; }
    public decimal Latitude { get; private set; }
    public decimal Longitude { get; private set; }
    public string Status { get; private set; } = "active";
    public Guid? IncidentId { get; private set; }
    public Guid? ResolvedById { get; private set; }
    public DateTime TriggeredAt { get; private set; } = DateTime.UtcNow;
    public DateTime? ResolvedAt { get; private set; }

    public User? User { get; private set; }
    public Vehicle? Vehicle { get; private set; }
    public Incident? Incident { get; private set; }
    public User? ResolvedBy { get; private set; }

    private PanicEvent() { }

    public static PanicEvent Create(Guid tenantId, Guid userId, decimal lat, decimal lng,
        Guid? vehicleId = null)
    {
        return new PanicEvent
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserId = userId,
            Latitude = lat,
            Longitude = lng,
            VehicleId = vehicleId,
            TriggeredAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Resolve(Guid resolvedById, Guid? incidentId = null)
    {
        Status = "resolved";
        ResolvedById = resolvedById;
        IncidentId = incidentId;
        ResolvedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }
}

public class Notification : TenantEntity
{
    public Guid RecipientId { get; private set; }
    public string Type { get; private set; } = string.Empty;
    public string Channel { get; private set; } = "push";
    public string Title { get; private set; } = string.Empty;
    public string Body { get; private set; } = string.Empty;
    public string DataJson { get; private set; } = "{}";
    public bool IsRead { get; private set; } = false;
    public DateTime? SentAt { get; private set; }
    public DateTime? ReadAt { get; private set; }

    public User? Recipient { get; private set; }

    private Notification() { }

    public static Notification Create(Guid tenantId, Guid recipientId, string type,
        string title, string body, string channel = "push", string? dataJson = null)
    {
        return new Notification
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            RecipientId = recipientId,
            Type = type,
            Title = title,
            Body = body,
            Channel = channel,
            DataJson = dataJson ?? "{}",
            CreatedAt = DateTime.UtcNow
        };
    }

    public void MarkSent() { SentAt = DateTime.UtcNow; }
    public void MarkRead() { IsRead = true; ReadAt = DateTime.UtcNow; }
}

public class AuditLog
{
    public long Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid? UserId { get; private set; }
    public string Action { get; private set; } = string.Empty;
    public string? EntityType { get; private set; }
    public string? EntityId { get; private set; }
    public string? OldValuesJson { get; private set; }
    public string? NewValuesJson { get; private set; }
    public string? IpAddress { get; private set; }
    public string? UserAgent { get; private set; }
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;

    private AuditLog() { }

    public static AuditLog Create(Guid tenantId, Guid? userId, string action,
        string? entityType = null, string? entityId = null,
        string? oldValues = null, string? newValues = null,
        string? ipAddress = null, string? userAgent = null)
    {
        return new AuditLog
        {
            TenantId = tenantId,
            UserId = userId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            OldValuesJson = oldValues,
            NewValuesJson = newValues,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            CreatedAt = DateTime.UtcNow
        };
    }
}

// Static helper for geo calculations used across entities
public static class GeoCalculator
{
    private const double EarthRadiusM = 6_371_000;

    public static double HaversineMeters(double lat1, double lng1, double lat2, double lng2)
    {
        var dLat = ToRad(lat2 - lat1);
        var dLng = ToRad(lng2 - lng1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
              + Math.Cos(ToRad(lat1)) * Math.Cos(ToRad(lat2))
              * Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
        return EarthRadiusM * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    public static double HaversineKm(double lat1, double lng1, double lat2, double lng2)
        => HaversineMeters(lat1, lng1, lat2, lng2) / 1000.0;

    private static double ToRad(double deg) => deg * Math.PI / 180.0;
}
