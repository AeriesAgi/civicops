using CivicOps.Domain.Enums;

namespace CivicOps.Domain.Entities;

public class Vehicle : TenantEntity
{
    public string Registration { get; private set; } = string.Empty;
    public string? Alias { get; private set; }
    public VehicleType Type { get; private set; }
    public string? Make { get; private set; }
    public string? Model { get; private set; }
    public int? Year { get; private set; }
    public string? Color { get; private set; }
    public string? Vin { get; private set; }
    public FuelType FuelType { get; private set; } = FuelType.Petrol;
    public decimal? FuelCapacityL { get; private set; }
    public decimal OdometerKm { get; private set; }
    public VehicleStatus Status { get; private set; } = VehicleStatus.Available;
    public int HealthScore { get; private set; } = 100;
    public Guid? RegionId { get; private set; }
    public Guid? AssignedDriverId { get; private set; }
    public string? GpsDeviceId { get; private set; }
    public string? TrackerProvider { get; private set; }
    public bool IsActive { get; private set; } = true;

    // Last known GPS (cached for quick access)
    public decimal? LastLatitude { get; private set; }
    public decimal? LastLongitude { get; private set; }
    public decimal? LastSpeedKmh { get; private set; }
    public decimal? LastHeadingDeg { get; private set; }
    public bool? LastIgnitionOn { get; private set; }
    public decimal? LastFuelLevelPct { get; private set; }
    public DateTime? LastGpsAt { get; private set; }

    // Navigation
    public Tenant? Tenant { get; private set; }
    public User? AssignedDriver { get; private set; }
    public Region? Region { get; private set; }
    public ICollection<VehicleGpsEvent> GpsEvents { get; private set; } = new List<VehicleGpsEvent>();
    public ICollection<Trip> Trips { get; private set; } = new List<Trip>();
    public ICollection<MaintenanceSchedule> MaintenanceSchedules { get; private set; } = new List<MaintenanceSchedule>();
    public ICollection<MaintenanceRecord> MaintenanceRecords { get; private set; } = new List<MaintenanceRecord>();
    public ICollection<DispatchAssignment> DispatchAssignments { get; private set; } = new List<DispatchAssignment>();

    private Vehicle() { }

    public static Vehicle Create(Guid tenantId, string registration, VehicleType type,
        string? make = null, string? model = null, int? year = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(registration);
        return new Vehicle
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Registration = registration.ToUpperInvariant().Trim(),
            Type = type,
            Make = make,
            Model = model,
            Year = year,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void UpdateDetails(string? alias, string? color, string? vin,
        FuelType fuelType, decimal? fuelCapacityL)
    {
        Alias = alias;
        Color = color;
        Vin = vin;
        FuelType = fuelType;
        FuelCapacityL = fuelCapacityL;
        UpdatedAt = DateTime.UtcNow;
    }

    public void AssignDriver(Guid? driverId)
    {
        AssignedDriverId = driverId;
        UpdatedAt = DateTime.UtcNow;
    }

    public void AssignToRegion(Guid? regionId)
    {
        RegionId = regionId;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetGpsDevice(string? deviceId, string? provider)
    {
        GpsDeviceId = deviceId;
        TrackerProvider = provider;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateStatus(VehicleStatus status)
    {
        Status = status;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateLastGps(decimal lat, decimal lng, decimal speedKmh,
        decimal? heading, bool? ignitionOn, decimal? fuelLevelPct)
    {
        LastLatitude = lat;
        LastLongitude = lng;
        LastSpeedKmh = speedKmh;
        LastHeadingDeg = heading;
        LastIgnitionOn = ignitionOn;
        LastFuelLevelPct = fuelLevelPct;
        LastGpsAt = DateTime.UtcNow;

        // Auto-update status based on GPS
        if (Status == VehicleStatus.Available || Status == VehicleStatus.Idle)
        {
            Status = speedKmh > 5 ? VehicleStatus.Active : VehicleStatus.Idle;
        }
    }

    public void UpdateOdometer(decimal odometerKm)
    {
        if (odometerKm > OdometerKm)
        {
            OdometerKm = odometerKm;
            UpdatedAt = DateTime.UtcNow;
        }
    }

    public void UpdateHealthScore(int score)
    {
        HealthScore = Math.Clamp(score, 0, 100);
        UpdatedAt = DateTime.UtcNow;
    }

    public void Deactivate() { IsActive = false; UpdatedAt = DateTime.UtcNow; }

    public bool IsOnline => LastGpsAt.HasValue && LastGpsAt.Value > DateTime.UtcNow.AddMinutes(-5);
}
