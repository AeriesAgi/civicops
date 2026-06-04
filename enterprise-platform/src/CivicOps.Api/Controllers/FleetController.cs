using CivicOps.Application.Commands.Fleet;
using CivicOps.Application.DTOs.Common;
using CivicOps.Application.DTOs.Fleet;
using CivicOps.Application.Queries.Fleet;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CivicOps.Api.Controllers;

[Authorize]
[Route("api/v1/fleet")]
public class FleetController : CivicOpsControllerBase
{
    private readonly IMediator _mediator;

    public FleetController(IMediator mediator) => _mediator = mediator;

    /// <summary>Get paginated list of vehicles for the current tenant.</summary>
    [HttpGet("vehicles")]
    public async Task<IActionResult> GetVehicles(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] string? status = null,
        [FromQuery] bool? isActive = true,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetVehiclesQuery(
            CurrentTenantId,
            new PagedQuery { Page = page, PageSize = pageSize, Search = search },
            isActive, status
        ), ct);

        return Ok(ApiResponse<PagedResult<VehicleListDto>>.Ok(result));
    }

    /// <summary>Get single vehicle with full detail.</summary>
    [HttpGet("vehicles/{id:guid}")]
    public async Task<IActionResult> GetVehicle(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetVehicleByIdQuery(CurrentTenantId, id), ct);
        return FromResult(result);
    }

    /// <summary>Create a new vehicle.</summary>
    [HttpPost("vehicles")]
    [Authorize(Policy = "FleetManager")]
    public async Task<IActionResult> CreateVehicle(
        [FromBody] CreateVehicleRequest req, CancellationToken ct)
    {
        var result = await _mediator.Send(
            new CreateVehicleCommand(CurrentTenantId, CurrentUserId, req), ct);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetVehicle), new { id = result.Value!.Id },
                ApiResponse<VehicleDto>.Ok(result.Value))
            : FromResult(result);
    }

    /// <summary>Update vehicle details.</summary>
    [HttpPut("vehicles/{id:guid}")]
    [Authorize(Policy = "FleetManager")]
    public async Task<IActionResult> UpdateVehicle(
        Guid id, [FromBody] UpdateVehicleRequest req, CancellationToken ct)
    {
        var result = await _mediator.Send(
            new UpdateVehicleCommand(CurrentTenantId, id, CurrentUserId, req), ct);
        return FromResult(result);
    }

    /// <summary>Assign or unassign a driver to a vehicle.</summary>
    [HttpPut("vehicles/{id:guid}/driver")]
    [Authorize(Policy = "FleetManager")]
    public async Task<IActionResult> AssignDriver(
        Guid id, [FromBody] Guid? driverId, CancellationToken ct)
    {
        var result = await _mediator.Send(
            new AssignDriverCommand(CurrentTenantId, id, driverId), ct);
        return FromResult(result);
    }

    /// <summary>Deactivate a vehicle (soft delete).</summary>
    [HttpDelete("vehicles/{id:guid}")]
    [Authorize(Policy = "FleetManager")]
    public async Task<IActionResult> DeactivateVehicle(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(
            new DeactivateVehicleCommand(CurrentTenantId, id), ct);
        return FromResult(result);
    }

    /// <summary>Get live positions for all vehicles in the fleet (from Redis cache).</summary>
    [HttpGet("live")]
    public async Task<IActionResult> GetLiveFleet(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetLiveFleetQuery(CurrentTenantId), ct);
        return Ok(ApiResponse<IEnumerable<LiveVehiclePositionDto>>.Ok(result));
    }

    /// <summary>Get GPS history for a vehicle within a time range.</summary>
    [HttpGet("vehicles/{id:guid}/gps")]
    public async Task<IActionResult> GetGpsHistory(
        Guid id,
        [FromQuery] DateTime from,
        [FromQuery] DateTime to,
        [FromQuery] int maxPoints = 1000,
        CancellationToken ct = default)
    {
        if (from >= to)
            return BadRequest(ApiResponse.Fail("'from' must be before 'to'."));
        if ((to - from).TotalDays > 7)
            return BadRequest(ApiResponse.Fail("Maximum query range is 7 days."));

        var result = await _mediator.Send(
            new GetVehicleGpsHistoryQuery(CurrentTenantId, id, from, to, maxPoints), ct);
        return Ok(ApiResponse<IEnumerable<GpsEventDto>>.Ok(result));
    }

    /// <summary>Get trip history for a vehicle.</summary>
    [HttpGet("vehicles/{id:guid}/trips")]
    public async Task<IActionResult> GetTrips(
        Guid id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(
            new GetVehicleTripsQuery(CurrentTenantId, id,
                new PagedQuery { Page = page, PageSize = pageSize }), ct);

        return Ok(ApiResponse<PagedResult<TripDto>>.Ok(result));
    }

    /// <summary>
    /// Ingest a single GPS event from a device or mobile app.
    /// High-throughput endpoint — minimal processing, fast response.
    /// </summary>
    [HttpPost("gps/ingest")]
    [EnableRateLimiting("gps")]
    [AllowAnonymous] // Device auth via device key header in production
    public async Task<IActionResult> IngestGps(
        [FromBody] GpsEventDto dto, CancellationToken ct)
    {
        var result = await _mediator.Send(new IngestGpsEventCommand(dto), ct);
        return result.IsSuccess ? Ok() : BadRequest(result.Error);
    }

    /// <summary>Ingest a batch of GPS events (for low-connectivity sync catch-up).</summary>
    [HttpPost("gps/bulk")]
    [EnableRateLimiting("gps")]
    [AllowAnonymous]
    public async Task<IActionResult> IngestGpsBatch(
        [FromBody] IEnumerable<GpsEventDto> events, CancellationToken ct)
    {
        var eventList = events.ToList();
        if (eventList.Count > 500)
            return BadRequest(ApiResponse.Fail("Maximum 500 events per batch."));

        var result = await _mediator.Send(new IngestGpsBatchCommand(eventList), ct);
        return FromResult(result);
    }
}
