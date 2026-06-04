namespace CivicOps.Domain.Entities;

public class VehicleGpsEvent
{
    public long Id { get; private set; }
    public Guid VehicleId { get; private set; }
    public Guid TenantId { get; private set; }
    public decimal Latitude { get; private set; }
    public decimal Longitude { get; private set; }
    public decimal? AltitudeM { get; private set; }
    public decimal SpeedKmh { get; private set; }
    public decimal? HeadingDeg { get; private set; }
    public decimal? AccuracyM { get; private set; }
    public short? Satellites { get; private set; }
    public bool? IgnitionOn { get; private set; }
    public decimal? FuelLevelPct { get; private set; }
    public decimal? OdometerKm { get; private set; }
    public string EventType { get; private set; } = "position";
    public DateTime RecordedAt { get; private set; }
    public DateTime ReceivedAt { get; private set; } = DateTime.UtcNow;
    public string? RawPayload { get; private set; }

    public Vehicle? Vehicle { get; private set; }

    private VehicleGpsEvent() { }

    public static VehicleGpsEvent Create(Guid vehicleId, Guid tenantId,
        decimal latitude, decimal longitude, decimal speedKmh,
        DateTime recordedAt, string eventType = "position",
        decimal? heading = null, decimal? altitude = null,
        bool? ignitionOn = null, decimal? fuelLevel = null,
        decimal? odometer = null, decimal? accuracy = null,
        short? satellites = null, string? rawPayload = null)
    {
        return new VehicleGpsEvent
        {
            VehicleId = vehicleId,
            TenantId = tenantId,
            Latitude = latitude,
            Longitude = longitude,
            SpeedKmh = speedKmh,
            HeadingDeg = heading,
            AltitudeM = altitude,
            IgnitionOn = ignitionOn,
            FuelLevelPct = fuelLevel,
            OdometerKm = odometer,
            AccuracyM = accuracy,
            Satellites = satellites,
            EventType = eventType,
            RecordedAt = recordedAt,
            ReceivedAt = DateTime.UtcNow,
            RawPayload = rawPayload
        };
    }
}
