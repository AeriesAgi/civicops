using CivicOps.Domain.Enums;
using MediatR;
using CivicOps.Application.DTOs.Common;

namespace CivicOps.Application.DTOs.Fleet;

public record VehicleDto(
    Guid Id,
    Guid TenantId,
    string Registration,
    string? Alias,
    string Type,
    string? Make,
    string? Model,
    int? Year,
    string? Color,
    string? Vin,
    string FuelType,
    decimal? FuelCapacityL,
    decimal OdometerKm,
    string Status,
    int HealthScore,
    Guid? RegionId,
    Guid? AssignedDriverId,
    string? DriverName,
    string? GpsDeviceId,
    bool IsActive,
    decimal? LastLatitude,
    decimal? LastLongitude,
    decimal? LastSpeedKmh,
    decimal? LastHeadingDeg,
    bool? LastIgnitionOn,
    decimal? LastFuelLevelPct,
    DateTime? LastGpsAt,
    bool IsOnline,
    DateTime CreatedAt
);

public record VehicleListDto(
    Guid Id,
    string Registration,
    string? Alias,
    string Type,
    string Status,
    int HealthScore,
    decimal? LastLatitude,
    decimal? LastLongitude,
    decimal? LastSpeedKmh,
    bool IsOnline,
    string? DriverName,
    DateTime? LastGpsAt
);

public record GpsEventDto(
    Guid VehicleId,
    Guid TenantId,
    decimal Latitude,
    decimal Longitude,
    decimal SpeedKmh,
    DateTime RecordedAt,
    string EventType = "position",
    decimal? HeadingDeg = null,
    decimal? AltitudeM = null,
    bool? IgnitionOn = null,
    decimal? FuelLevelPct = null,
    decimal? OdometerKm = null,
    decimal? AccuracyM = null,
    short? Satellites = null,
    string? RawPayload = null
);

public record LiveVehiclePositionDto(
    Guid VehicleId,
    string Registration,
    string? Alias,
    string Type,
    string Status,
    decimal Latitude,
    decimal Longitude,
    decimal SpeedKmh,
    decimal? HeadingDeg,
    bool? IgnitionOn,
    decimal? FuelLevelPct,
    string? DriverName,
    Guid? DriverId,
    DateTime UpdatedAt
);

public record TripDto(
    Guid Id,
    Guid VehicleId,
    string Registration,
    Guid? DriverId,
    string? DriverName,
    decimal? StartLat,
    decimal? StartLng,
    decimal? EndLat,
    decimal? EndLng,
    decimal? DistanceKm,
    int? DurationMinutes,
    decimal? MaxSpeedKmh,
    decimal? AvgSpeedKmh,
    int IdleMinutes,
    decimal? FuelUsedL,
    DateTime StartedAt,
    DateTime? EndedAt,
    string Status
);

public record CreateVehicleRequest(
    string Registration,
    VehicleType Type,
    string? Alias = null,
    string? Make = null,
    string? Model = null,
    int? Year = null,
    string? Color = null,
    string? Vin = null,
    FuelType FuelType = FuelType.Diesel,
    decimal? FuelCapacityL = null,
    Guid? RegionId = null,
    string? GpsDeviceId = null,
    string? TrackerProvider = null
);

public record UpdateVehicleRequest(
    string? Alias,
    string? Make,
    string? Model,
    int? Year,
    string? Color,
    string? Vin,
    FuelType FuelType,
    decimal? FuelCapacityL,
    Guid? RegionId,
    string? GpsDeviceId,
    string? TrackerProvider
);

namespace CivicOps.Application.Commands.Fleet;

using CivicOps.Application.DTOs.Fleet;

public record CreateVehicleCommand(Guid TenantId, Guid CreatedById, CreateVehicleRequest Request)
    : IRequest<Result<VehicleDto>>;

public record UpdateVehicleCommand(Guid TenantId, Guid VehicleId, Guid UpdatedById, UpdateVehicleRequest Request)
    : IRequest<Result<VehicleDto>>;

public record AssignDriverCommand(Guid TenantId, Guid VehicleId, Guid? DriverId)
    : IRequest<Result>;

public record IngestGpsEventCommand(GpsEventDto GpsEvent)
    : IRequest<Result>;

public record IngestGpsBatchCommand(IEnumerable<GpsEventDto> GpsEvents)
    : IRequest<Result>;

public record DeactivateVehicleCommand(Guid TenantId, Guid VehicleId)
    : IRequest<Result>;

namespace CivicOps.Application.Queries.Fleet;

using CivicOps.Application.DTOs.Fleet;

public record GetVehiclesQuery(Guid TenantId, PagedQuery Paged, bool? IsActive = null, string? Status = null)
    : IRequest<PagedResult<VehicleListDto>>;

public record GetVehicleByIdQuery(Guid TenantId, Guid VehicleId)
    : IRequest<Result<VehicleDto>>;

public record GetLiveFleetQuery(Guid TenantId)
    : IRequest<IEnumerable<LiveVehiclePositionDto>>;

public record GetVehicleGpsHistoryQuery(
    Guid TenantId,
    Guid VehicleId,
    DateTime From,
    DateTime To,
    int MaxPoints = 1000
) : IRequest<IEnumerable<GpsEventDto>>;

public record GetVehicleTripsQuery(Guid TenantId, Guid VehicleId, PagedQuery Paged)
    : IRequest<PagedResult<TripDto>>;
