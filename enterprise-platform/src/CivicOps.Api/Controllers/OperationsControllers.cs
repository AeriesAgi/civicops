using CivicOps.Application.Commands.Dispatch;
using CivicOps.Application.Commands.Incidents;
using CivicOps.Application.Commands.Maintenance;
using CivicOps.Application.DTOs.Analytics;
using CivicOps.Application.DTOs.Common;
using CivicOps.Application.DTOs.Dispatch;
using CivicOps.Application.DTOs.Incidents;
using CivicOps.Application.DTOs.Maintenance;
using CivicOps.Application.Queries.Analytics;
using CivicOps.Application.Queries.Dispatch;
using CivicOps.Application.Queries.Incidents;
using CivicOps.Application.Queries.Maintenance;
using CivicOps.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CivicOps.Api.Controllers;

// ═══════════════════════════════════════════════════════════════════
// INCIDENTS CONTROLLER
// ═══════════════════════════════════════════════════════════════════

[Authorize]
[Route("api/v1/incidents")]
public class IncidentsController : CivicOpsControllerBase
{
    private readonly IMediator _mediator;
    public IncidentsController(IMediator mediator) => _mediator = mediator;

    /// <summary>Get paginated incident list with full filtering.</summary>
    [HttpGet]
    public async Task<IActionResult> GetIncidents(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] IncidentStatus? status = null,
        [FromQuery] IncidentPriority? priority = null,
        [FromQuery] IncidentCategory? category = null,
        [FromQuery] Guid? assignedToId = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetIncidentsQuery(
            CurrentTenantId,
            new PagedQuery { Page = page, PageSize = pageSize, Search = search },
            status, priority, category, assignedToId, from, to
        ), ct);

        return Ok(ApiResponse<PagedResult<IncidentListDto>>.Ok(result));
    }

    /// <summary>Get open incidents (for dashboard feed).</summary>
    [HttpGet("open")]
    public async Task<IActionResult> GetOpenIncidents(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetOpenIncidentsQuery(CurrentTenantId), ct);
        return Ok(ApiResponse<IEnumerable<IncidentListDto>>.Ok(result));
    }

    /// <summary>Get incidents at SLA risk.</summary>
    [HttpGet("sla-at-risk")]
    public async Task<IActionResult> GetSlaAtRisk(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetSlaAtRiskIncidentsQuery(CurrentTenantId), ct);
        return Ok(ApiResponse<IEnumerable<IncidentListDto>>.Ok(result));
    }

    /// <summary>Get single incident with full detail, updates, and media.</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetIncident(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetIncidentByIdQuery(CurrentTenantId, id), ct);
        return FromResult(result);
    }

    /// <summary>Get incident full response timeline.</summary>
    [HttpGet("{id:guid}/timeline")]
    public async Task<IActionResult> GetTimeline(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetIncidentTimelineQuery(CurrentTenantId, id), ct);
        return FromResult(result);
    }

    /// <summary>Create a new incident.</summary>
    [HttpPost]
    public async Task<IActionResult> CreateIncident(
        [FromBody] CreateIncidentRequest req, CancellationToken ct)
    {
        var result = await _mediator.Send(
            new CreateIncidentCommand(CurrentTenantId, CurrentUserId, req), ct);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetIncident), new { id = result.Value!.Id },
                ApiResponse<IncidentDto>.Ok(result.Value))
            : FromResult(result);
    }

    /// <summary>Update incident details.</summary>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateIncident(
        Guid id, [FromBody] UpdateIncidentRequest req, CancellationToken ct)
    {
        var result = await _mediator.Send(
            new UpdateIncidentCommand(CurrentTenantId, id, CurrentUserId, req), ct);
        return FromResult(result);
    }

    /// <summary>Assign unit and/or officer to incident.</summary>
    [HttpPost("{id:guid}/assign")]
    [Authorize(Policy = "Dispatcher")]
    public async Task<IActionResult> Assign(
        Guid id, [FromBody] AssignIncidentRequest req, CancellationToken ct)
    {
        var result = await _mediator.Send(
            new AssignIncidentCommand(CurrentTenantId, id, CurrentUserId,
                req.UserId, req.VehicleId), ct);
        return FromResult(result);
    }

    /// <summary>Escalate incident to supervisor.</summary>
    [HttpPost("{id:guid}/escalate")]
    public async Task<IActionResult> Escalate(
        Guid id, [FromBody] EscalateIncidentRequest req, CancellationToken ct)
    {
        var result = await _mediator.Send(
            new EscalateIncidentCommand(CurrentTenantId, id, CurrentUserId,
                req.EscalateToUserId, req.Reason), ct);
        return FromResult(result);
    }

    /// <summary>Add a text update/note to an incident.</summary>
    [HttpPost("{id:guid}/updates")]
    public async Task<IActionResult> AddUpdate(
        Guid id, [FromBody] AddIncidentUpdateRequest req, CancellationToken ct)
    {
        var result = await _mediator.Send(
            new AddIncidentUpdateCommand(CurrentTenantId, id, CurrentUserId,
                req.Type, req.Note, req.IsInternal, null), ct);
        return FromResult(result);
    }

    /// <summary>Upload image, video, or voice note to incident.</summary>
    [HttpPost("{id:guid}/media")]
    [RequestSizeLimit(50 * 1024 * 1024)] // 50MB limit
    public async Task<IActionResult> UploadMedia(
        Guid id, IFormFile file, CancellationToken ct)
    {
        if (file.Length == 0)
            return BadRequest(ApiResponse.Fail("No file provided."));

        var result = await _mediator.Send(
            new UploadIncidentMediaCommand(
                CurrentTenantId, id, CurrentUserId,
                file.OpenReadStream(), file.FileName, file.ContentType), ct);
        return FromResult(result);
    }

    /// <summary>Close an incident with optional resolution note.</summary>
    [HttpPost("{id:guid}/close")]
    [Authorize(Policy = "Supervisor")]
    public async Task<IActionResult> Close(
        Guid id, [FromBody] CloseIncidentRequest req, CancellationToken ct)
    {
        var result = await _mediator.Send(
            new CloseIncidentCommand(CurrentTenantId, id, CurrentUserId, req.ResolutionNote), ct);
        return FromResult(result);
    }
}

// ═══════════════════════════════════════════════════════════════════
// DISPATCH CONTROLLER
// ═══════════════════════════════════════════════════════════════════

[Authorize(Policy = "Dispatcher")]
[Route("api/v1/dispatch")]
public class DispatchController : CivicOpsControllerBase
{
    private readonly IMediator _mediator;
    public DispatchController(IMediator mediator) => _mediator = mediator;

    /// <summary>Get current active dispatch queue.</summary>
    [HttpGet("queue")]
    public async Task<IActionResult> GetQueue(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetDispatchQueueQuery(CurrentTenantId), ct);
        return Ok(ApiResponse<IEnumerable<DispatchQueueItemDto>>.Ok(result));
    }

    /// <summary>Get AI-powered nearest-unit dispatch recommendation.</summary>
    [HttpPost("recommend")]
    public async Task<IActionResult> GetRecommendation(
        [FromBody] NearestUnitRequest req, CancellationToken ct)
    {
        var result = await _mediator.Send(
            new GetDispatchRecommendationCommand(CurrentTenantId, req), ct);
        return FromResult(result);
    }

    /// <summary>Create a dispatch assignment (after dispatcher confirms).</summary>
    [HttpPost("assign")]
    public async Task<IActionResult> CreateAssignment(
        [FromBody] CreateAssignmentRequest req, CancellationToken ct)
    {
        var result = await _mediator.Send(
            new CreateAssignmentCommand(CurrentTenantId, CurrentUserId, req), ct);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetAssignment), new { id = result.Value!.Id },
                ApiResponse<DispatchAssignmentDto>.Ok(result.Value))
            : FromResult(result);
    }

    /// <summary>Get a specific dispatch assignment.</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetAssignment(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetAssignmentByIdQuery(CurrentTenantId, id), ct);
        return FromResult(result);
    }

    /// <summary>Unit acknowledges dispatch.</summary>
    [HttpPut("{id:guid}/acknowledge")]
    [Authorize] // Any authenticated user (field officer)
    public async Task<IActionResult> Acknowledge(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(
            new AcknowledgeDispatchCommand(CurrentTenantId, id, CurrentUserId), ct);
        return FromResult(result);
    }

    /// <summary>Unit marks arrival on scene.</summary>
    [HttpPut("{id:guid}/arrive")]
    [Authorize]
    public async Task<IActionResult> Arrive(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(
            new MarkArrivedCommand(CurrentTenantId, id, CurrentUserId), ct);
        return FromResult(result);
    }

    /// <summary>Complete a dispatch assignment.</summary>
    [HttpPut("{id:guid}/complete")]
    [Authorize]
    public async Task<IActionResult> Complete(
        Guid id, [FromBody] string? notes, CancellationToken ct)
    {
        var result = await _mediator.Send(
            new CompleteAssignmentCommand(CurrentTenantId, id, CurrentUserId, notes), ct);
        return FromResult(result);
    }

    /// <summary>Cancel a dispatch assignment.</summary>
    [HttpPut("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(
        Guid id, [FromBody] string? reason, CancellationToken ct)
    {
        var result = await _mediator.Send(
            new CancelAssignmentCommand(CurrentTenantId, id, CurrentUserId, reason), ct);
        return FromResult(result);
    }

    /// <summary>Get dispatch history with date range filtering.</summary>
    [HttpGet("history")]
    public async Task<IActionResult> GetHistory(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetDispatchHistoryQuery(
            CurrentTenantId,
            new PagedQuery { Page = page, PageSize = pageSize },
            from, to
        ), ct);
        return Ok(ApiResponse<PagedResult<DispatchAssignmentDto>>.Ok(result));
    }
}

// ═══════════════════════════════════════════════════════════════════
// ANALYTICS CONTROLLER
// ═══════════════════════════════════════════════════════════════════

[Authorize]
[Route("api/v1/analytics")]
public class AnalyticsController : CivicOpsControllerBase
{
    private readonly IMediator _mediator;
    public AnalyticsController(IMediator mediator) => _mediator = mediator;

    /// <summary>Real-time operational KPI dashboard data.</summary>
    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetDashboardKpisQuery(CurrentTenantId), ct);
        return Ok(ApiResponse<DashboardKpiDto>.Ok(result));
    }

    /// <summary>Response time analytics for a date range.</summary>
    [HttpGet("response-times")]
    public async Task<IActionResult> GetResponseTimes(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        CancellationToken ct = default)
    {
        var dateFrom = from ?? DateTime.UtcNow.AddDays(-30);
        var dateTo = to ?? DateTime.UtcNow;
        var result = await _mediator.Send(
            new GetResponseTimeAnalyticsQuery(CurrentTenantId, dateFrom, dateTo), ct);
        return Ok(ApiResponse<ResponseTimeAnalyticsDto>.Ok(result));
    }

    /// <summary>Fleet utilization metrics.</summary>
    [HttpGet("fleet-utilization")]
    [Authorize(Policy = "FleetManager")]
    public async Task<IActionResult> GetFleetUtilization(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        CancellationToken ct = default)
    {
        var dateFrom = from ?? DateTime.UtcNow.AddDays(-30);
        var dateTo = to ?? DateTime.UtcNow;
        var result = await _mediator.Send(
            new GetFleetUtilizationQuery(CurrentTenantId, dateFrom, dateTo), ct);
        return Ok(ApiResponse<FleetUtilizationDto>.Ok(result));
    }

    /// <summary>Incident trend analysis.</summary>
    [HttpGet("incidents")]
    public async Task<IActionResult> GetIncidentTrends(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(
            new GetIncidentTrendsQuery(CurrentTenantId,
                from ?? DateTime.UtcNow.AddDays(-30),
                to ?? DateTime.UtcNow), ct);
        return Ok(ApiResponse<IncidentTrendsDto>.Ok(result));
    }

    /// <summary>Geospatial incident heatmap data.</summary>
    [HttpGet("heatmap")]
    public async Task<IActionResult> GetHeatmap(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] string? category = null,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(
            new GetIncidentHeatmapQuery(CurrentTenantId,
                from ?? DateTime.UtcNow.AddDays(-30),
                to ?? DateTime.UtcNow,
                category), ct);
        return Ok(ApiResponse<IEnumerable<HeatmapPointDto>>.Ok(result));
    }

    /// <summary>Operator performance metrics.</summary>
    [HttpGet("operator-performance")]
    [Authorize(Policy = "Supervisor")]
    public async Task<IActionResult> GetOperatorPerformance(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(
            new GetOperatorPerformanceQuery(CurrentTenantId,
                from ?? DateTime.UtcNow.AddDays(-30),
                to ?? DateTime.UtcNow), ct);
        return Ok(ApiResponse<IEnumerable<OperatorPerformanceDto>>.Ok(result));
    }

    /// <summary>SLA compliance report.</summary>
    [HttpGet("sla")]
    [Authorize(Policy = "Supervisor")]
    public async Task<IActionResult> GetSlaCompliance(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(
            new GetSlaComplianceQuery(CurrentTenantId,
                from ?? DateTime.UtcNow.AddDays(-30),
                to ?? DateTime.UtcNow), ct);
        return Ok(ApiResponse<SlaComplianceDto>.Ok(result));
    }
}

// ═══════════════════════════════════════════════════════════════════
// MAINTENANCE CONTROLLER
// ═══════════════════════════════════════════════════════════════════

[Authorize]
[Route("api/v1/maintenance")]
public class MaintenanceController : CivicOpsControllerBase
{
    private readonly IMediator _mediator;
    public MaintenanceController(IMediator mediator) => _mediator = mediator;

    /// <summary>Get maintenance schedules, optionally filtered by vehicle or due status.</summary>
    [HttpGet]
    public async Task<IActionResult> GetSchedules(
        [FromQuery] Guid? vehicleId = null,
        [FromQuery] bool? dueOnly = null,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(
            new GetMaintenanceSchedulesQuery(CurrentTenantId, vehicleId, dueOnly), ct);
        return Ok(ApiResponse<IEnumerable<MaintenanceScheduleDto>>.Ok(result));
    }

    /// <summary>Get maintenance alerts — due and overdue items.</summary>
    [HttpGet("alerts")]
    public async Task<IActionResult> GetAlerts(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetMaintenanceAlertsQuery(CurrentTenantId), ct);
        return Ok(ApiResponse<IEnumerable<MaintenanceAlertDto>>.Ok(result));
    }

    /// <summary>Get maintenance records for a specific vehicle.</summary>
    [HttpGet("vehicles/{vehicleId:guid}/records")]
    public async Task<IActionResult> GetRecords(
        Guid vehicleId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(
            new GetMaintenanceRecordsQuery(CurrentTenantId, vehicleId,
                new PagedQuery { Page = page, PageSize = pageSize }), ct);
        return Ok(ApiResponse<PagedResult<MaintenanceRecordDto>>.Ok(result));
    }

    /// <summary>Create a new maintenance schedule.</summary>
    [HttpPost]
    [Authorize(Policy = "FleetManager")]
    public async Task<IActionResult> CreateSchedule(
        [FromBody] CreateMaintenanceScheduleRequest req, CancellationToken ct)
    {
        var result = await _mediator.Send(
            new CreateMaintenanceScheduleCommand(CurrentTenantId, CurrentUserId, req), ct);
        return FromResult(result);
    }

    /// <summary>Log a completed maintenance service record.</summary>
    [HttpPost("records")]
    [Authorize(Policy = "FleetManager")]
    public async Task<IActionResult> LogRecord(
        [FromBody] LogMaintenanceRecordRequest req, CancellationToken ct)
    {
        var result = await _mediator.Send(
            new LogMaintenanceRecordCommand(CurrentTenantId, CurrentUserId, req), ct);
        return FromResult(result);
    }

    /// <summary>Run AI predictive maintenance analysis for a vehicle.</summary>
    [HttpPost("vehicles/{vehicleId:guid}/predict")]
    [Authorize(Policy = "FleetManager")]
    public async Task<IActionResult> PredictMaintenance(Guid vehicleId, CancellationToken ct)
    {
        var result = await _mediator.Send(
            new RunPredictiveAnalysisCommand(CurrentTenantId, vehicleId), ct);
        return FromResult(result);
    }
}
