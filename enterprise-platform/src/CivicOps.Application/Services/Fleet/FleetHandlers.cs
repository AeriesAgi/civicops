using CivicOps.Application.Commands.Fleet;
using CivicOps.Application.DTOs.Common;
using CivicOps.Application.DTOs.Fleet;
using CivicOps.Application.Interfaces;
using CivicOps.Application.Queries.Fleet;
using CivicOps.Domain.Entities;
using CivicOps.Domain.Enums;
using CivicOps.Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CivicOps.Application.Services.Fleet;

public class CreateVehicleHandler : IRequestHandler<CreateVehicleCommand, Result<VehicleDto>>
{
    private readonly IUnitOfWork _uow;

    public CreateVehicleHandler(IUnitOfWork uow) => _uow = uow;

    public async Task<Result<VehicleDto>> Handle(CreateVehicleCommand request, CancellationToken ct)
    {
        var existing = await _uow.Vehicles.GetByRegistrationAsync(
            request.TenantId, request.Request.Registration, ct);

        if (existing is not null)
            return Result.Failure<VehicleDto>($"Vehicle '{request.Request.Registration}' already exists.");

        var vehicle = Vehicle.Create(
            request.TenantId,
            request.Request.Registration,
            request.Request.Type,
            request.Request.Make,
            request.Request.Model,
            request.Request.Year
        );

        vehicle.UpdateDetails(
            request.Request.Alias,
            request.Request.Color,
            request.Request.Vin,
            request.Request.FuelType,
            request.Request.FuelCapacityL
        );

        if (request.Request.RegionId.HasValue)
            vehicle.AssignToRegion(request.Request.RegionId);

        if (!string.IsNullOrEmpty(request.Request.GpsDeviceId))
            vehicle.SetGpsDevice(request.Request.GpsDeviceId, request.Request.TrackerProvider);

        vehicle.SetCreatedBy(request.CreatedById.ToString());

        await _uow.Vehicles.AddAsync(vehicle, ct);
        await _uow.SaveChangesAsync(ct);

        return Result.Success(MapToDto(vehicle));
    }

    public static VehicleDto MapToDto(Vehicle v, string? driverName = null) => new(
        v.Id, v.TenantId, v.Registration, v.Alias, v.Type.ToString(),
        v.Make, v.Model, v.Year, v.Color, v.Vin, v.FuelType.ToString(),
        v.FuelCapacityL, v.OdometerKm, v.Status.ToString(), v.HealthScore,
        v.RegionId, v.AssignedDriverId, driverName, v.GpsDeviceId, v.IsActive,
        v.LastLatitude, v.LastLongitude, v.LastSpeedKmh, v.LastHeadingDeg,
        v.LastIgnitionOn, v.LastFuelLevelPct, v.LastGpsAt, v.IsOnline, v.CreatedAt
    );
}

public class GetVehiclesHandler : IRequestHandler<GetVehiclesQuery, PagedResult<VehicleListDto>>
{
    private readonly IUnitOfWork _uow;

    public GetVehiclesHandler(IUnitOfWork uow) => _uow = uow;

    public async Task<PagedResult<VehicleListDto>> Handle(GetVehiclesQuery request, CancellationToken ct)
    {
        var vehicles = await _uow.Vehicles.GetByTenantAsync(
            request.TenantId,
            request.IsActive ?? true,
            ct
        );

        var filtered = vehicles.AsQueryable();

        if (!string.IsNullOrEmpty(request.Status) && Enum.TryParse<VehicleStatus>(request.Status, out var status))
            filtered = filtered.Where(v => v.Status == status);

        if (!string.IsNullOrEmpty(request.Paged.Search))
        {
            var search = request.Paged.Search.ToLower();
            filtered = filtered.Where(v =>
                v.Registration.ToLower().Contains(search) ||
                (v.Alias != null && v.Alias.ToLower().Contains(search)));
        }

        var totalCount = filtered.Count();
        var items = filtered
            .Skip(request.Paged.Skip)
            .Take(request.Paged.PageSize)
            .Select(v => new VehicleListDto(
                v.Id, v.Registration, v.Alias, v.Type.ToString(), v.Status.ToString(),
                v.HealthScore, v.LastLatitude, v.LastLongitude, v.LastSpeedKmh,
                v.IsOnline, null, v.LastGpsAt
            ));

        return PagedResult<VehicleListDto>.Create(items, totalCount, request.Paged.Page, request.Paged.PageSize);
    }
}

public class GetLiveFleetHandler : IRequestHandler<GetLiveFleetQuery, IEnumerable<LiveVehiclePositionDto>>
{
    private readonly ILiveFleetCache _fleetCache;

    public GetLiveFleetHandler(ILiveFleetCache fleetCache) => _fleetCache = fleetCache;

    public async Task<IEnumerable<LiveVehiclePositionDto>> Handle(GetLiveFleetQuery request, CancellationToken ct)
    {
        var positions = await _fleetCache.GetAllPositionsAsync(request.TenantId, ct);
        return positions.Select(p => new LiveVehiclePositionDto(
            p.VehicleId, p.Registration, p.Alias, p.VehicleType,
            p.Status, p.Latitude, p.Longitude, p.SpeedKmh, p.HeadingDeg,
            p.IgnitionOn, p.FuelLevelPct, p.DriverName, p.AssignedDriverId, p.UpdatedAt
        ));
    }
}

public class IngestGpsEventHandler : IRequestHandler<IngestGpsEventCommand, Result>
{
    private readonly IUnitOfWork _uow;
    private readonly ILiveFleetCache _fleetCache;
    private readonly ISignalRService _signalR;
    private readonly ILogger<IngestGpsEventHandler> _logger;

    public IngestGpsEventHandler(IUnitOfWork uow, ILiveFleetCache fleetCache,
        ISignalRService signalR, ILogger<IngestGpsEventHandler> logger)
    {
        _uow = uow;
        _fleetCache = fleetCache;
        _signalR = signalR;
        _logger = logger;
    }

    public async Task<Result> Handle(IngestGpsEventCommand request, CancellationToken ct)
    {
        var dto = request.GpsEvent;

        var vehicle = await _uow.Vehicles.GetByIdAsync(dto.VehicleId, ct);
        if (vehicle is null)
        {
            _logger.LogWarning("GPS event for unknown vehicle {VehicleId}", dto.VehicleId);
            return Result.Failure("Vehicle not found.");
        }

        // Update in-memory vehicle state
        vehicle.UpdateLastGps(dto.Latitude, dto.Longitude, dto.SpeedKmh,
            dto.HeadingDeg, dto.IgnitionOn, dto.FuelLevelPct);

        if (dto.OdometerKm.HasValue)
            vehicle.UpdateOdometer(dto.OdometerKm.Value);

        // Persist GPS event
        var gpsEvent = VehicleGpsEvent.Create(
            dto.VehicleId, dto.TenantId,
            dto.Latitude, dto.Longitude, dto.SpeedKmh,
            dto.RecordedAt, dto.EventType,
            dto.HeadingDeg, dto.AltitudeM, dto.IgnitionOn,
            dto.FuelLevelPct, dto.OdometerKm, dto.AccuracyM,
            dto.Satellites, dto.RawPayload
        );

        await _uow.GpsEvents.AddEventAsync(gpsEvent, ct);
        await _uow.Vehicles.UpdateAsync(vehicle, ct);
        await _uow.SaveChangesAsync(ct);

        // Update live cache
        var cacheItem = new VehiclePositionCacheItem(
            vehicle.Id, vehicle.TenantId, vehicle.Registration, vehicle.Alias,
            dto.Latitude, dto.Longitude, dto.SpeedKmh, dto.HeadingDeg,
            dto.IgnitionOn, dto.FuelLevelPct, vehicle.Status.ToString(),
            vehicle.Type.ToString(), vehicle.AssignedDriverId, null, DateTime.UtcNow
        );
        await _fleetCache.SetVehiclePositionAsync(vehicle.Id, cacheItem, ct);

        // Broadcast to connected clients immediately
        await _signalR.SendGpsUpdateAsync(vehicle.TenantId, new
        {
            vehicleId = vehicle.Id,
            registration = vehicle.Registration,
            alias = vehicle.Alias,
            latitude = dto.Latitude,
            longitude = dto.Longitude,
            speedKmh = dto.SpeedKmh,
            headingDeg = dto.HeadingDeg,
            ignitionOn = dto.IgnitionOn,
            fuelLevelPct = dto.FuelLevelPct,
            status = vehicle.Status.ToString(),
            timestamp = dto.RecordedAt
        }, ct);

        return Result.Success();
    }
}

public class IngestGpsBatchHandler : IRequestHandler<IngestGpsBatchCommand, Result>
{
    private readonly IMediator _mediator;

    public IngestGpsBatchHandler(IMediator mediator) => _mediator = mediator;

    public async Task<Result> Handle(IngestGpsBatchCommand request, CancellationToken ct)
    {
        var tasks = request.GpsEvents
            .Select(e => _mediator.Send(new IngestGpsEventCommand(e), ct));

        await Task.WhenAll(tasks);
        return Result.Success();
    }
}

public class GetVehicleGpsHistoryHandler : IRequestHandler<GetVehicleGpsHistoryQuery, IEnumerable<GpsEventDto>>
{
    private readonly IUnitOfWork _uow;

    public GetVehicleGpsHistoryHandler(IUnitOfWork uow) => _uow = uow;

    public async Task<IEnumerable<GpsEventDto>> Handle(GetVehicleGpsHistoryQuery request, CancellationToken ct)
    {
        var events = await _uow.GpsEvents.GetHistoryAsync(request.VehicleId, request.From, request.To, ct);

        // Downsample if needed to MaxPoints
        var list = events.ToList();
        if (list.Count > request.MaxPoints)
        {
            var step = list.Count / request.MaxPoints;
            list = list.Where((_, i) => i % step == 0).ToList();
        }

        return list.Select(e => new GpsEventDto(
            e.VehicleId, e.TenantId, e.Latitude, e.Longitude, e.SpeedKmh,
            e.RecordedAt, e.EventType, e.HeadingDeg, e.AltitudeM,
            e.IgnitionOn, e.FuelLevelPct, e.OdometerKm, e.AccuracyM, e.Satellites
        ));
    }
}
