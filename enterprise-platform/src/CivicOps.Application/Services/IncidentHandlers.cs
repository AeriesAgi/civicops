using CivicOps.Application.Commands.Incidents;
using CivicOps.Application.DTOs.Common;
using CivicOps.Application.DTOs.Incidents;
using CivicOps.Application.Interfaces;
using CivicOps.Application.Queries.Incidents;
using CivicOps.Domain.Entities;
using CivicOps.Domain.Enums;
using CivicOps.Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CivicOps.Application.Services;

public class CreateIncidentHandler : IRequestHandler<CreateIncidentCommand, Result<IncidentDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ISignalRService _signalR;
    private readonly ILLMProvider _llm;
    private readonly ILogger<CreateIncidentHandler> _logger;

    public CreateIncidentHandler(IUnitOfWork uow, ISignalRService signalR,
        ILLMProvider llm, ILogger<CreateIncidentHandler> logger)
    {
        _uow = uow;
        _signalR = signalR;
        _llm = llm;
        _logger = logger;
    }

    public async Task<Result<IncidentDto>> Handle(CreateIncidentCommand request, CancellationToken ct)
    {
        var refNo = await _uow.Incidents.GenerateReferenceNoAsync(request.TenantId, ct);

        var incident = Incident.Create(
            request.TenantId,
            refNo,
            request.Request.Title,
            request.Request.Category,
            request.Request.Priority,
            request.CreatedById,
            request.Request.Latitude,
            request.Request.Longitude,
            request.Request.Address,
            request.Request.Description
        );

        if (request.Request.Tags is { Count: > 0 })
            foreach (var tag in request.Request.Tags)
                incident.AddTag(tag);

        // Set SLA based on priority
        incident.SetSlaTarget(request.Request.Priority switch
        {
            IncidentPriority.Critical => 5,
            IncidentPriority.High => 10,
            IncidentPriority.Medium => 20,
            IncidentPriority.Low => 45,
            _ => 60
        });

        incident.SetCreatedBy(request.CreatedById.ToString());

        await _uow.Incidents.AddAsync(incident, ct);
        await _uow.SaveChangesAsync(ct);

        // Async AI analysis (fire and forget, don't block response)
        _ = Task.Run(async () =>
        {
            try
            {
                await RunAiAnalysisAsync(incident, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AI analysis failed for incident {Id}", incident.Id);
            }
        }, ct);

        var dto = await BuildDtoAsync(incident, ct);

        await _signalR.SendIncidentCreatedAsync(request.TenantId, dto, ct);

        return Result.Success(dto);
    }

    private async Task RunAiAnalysisAsync(Incident incident, CancellationToken ct)
    {
        if (!_llm.IsAvailable) return;

        const string systemPrompt = """
            You are an operational intelligence assistant for a security dispatch system.
            Analyze the incident and return a JSON object with:
            {
              "summary": "2-3 sentence operational summary",
              "priorityScore": 0.0-1.0,
              "suggestedTags": ["TAG1", "TAG2"],
              "riskLevel": "critical|high|medium|low"
            }
            Be concise and operationally focused.
            """;

        var userPrompt = $"""
            Incident: {incident.Title}
            Category: {incident.Category}
            Priority: {incident.Priority}
            Description: {incident.Description ?? "No description provided"}
            Location: {incident.Address ?? $"{incident.Latitude},{incident.Longitude}"}
            """;

        var response = await _llm.CompleteAsync(systemPrompt, userPrompt, ct);

        // Parse JSON response (simplified)
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(response);
            var root = doc.RootElement;
            var summary = root.TryGetProperty("summary", out var s) ? s.GetString() : null;
            var scoreStr = root.TryGetProperty("priorityScore", out var ps) ? (decimal?)ps.GetDecimal() : null;

            incident.SetAiAnalysis(summary, scoreStr);

            // Reload and save without concurrency issues
            var fresh = await _uow.Incidents.GetByIdAsync(incident.Id, ct);
            if (fresh is not null)
            {
                fresh.SetAiAnalysis(summary, scoreStr);
                await _uow.Incidents.UpdateAsync(fresh, ct);
                await _uow.SaveChangesAsync(ct);
            }
        }
        catch { /* AI parse failure is non-critical */ }
    }

    private async Task<IncidentDto> BuildDtoAsync(Incident incident, CancellationToken ct)
    {
        return new IncidentDto(
            incident.Id, incident.TenantId, incident.ReferenceNo, incident.Title,
            incident.Description, incident.Category.ToString(), incident.SubCategory,
            (int)incident.Priority, incident.Severity.ToString(), incident.Status.ToString(),
            incident.Latitude, incident.Longitude, incident.Address, incident.RegionId,
            incident.ReportedById, null, incident.AssignedToId, null,
            incident.AssignedVehicleId, null, incident.EscalatedToId, null,
            incident.SlaTargetMinutes, incident.SlaBreached, incident.IsSlaAtRisk(),
            incident.ResponseTimeMin, incident.ResolutionTimeMin,
            incident.AiSummary, incident.AiPriorityScore,
            incident.Tags, incident.OpenedAt, incident.FirstResponseAt,
            incident.ResolvedAt, incident.ClosedAt, incident.MinutesOpen,
            Enumerable.Empty<IncidentUpdateDto>()
        );
    }
}

public class AssignIncidentHandler : IRequestHandler<AssignIncidentCommand, Result<IncidentDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ISignalRService _signalR;

    public AssignIncidentHandler(IUnitOfWork uow, ISignalRService signalR)
    {
        _uow = uow;
        _signalR = signalR;
    }

    public async Task<Result<IncidentDto>> Handle(AssignIncidentCommand request, CancellationToken ct)
    {
        var incident = await _uow.Incidents.GetByIdAsync(request.IncidentId, ct);
        if (incident is null || incident.TenantId != request.TenantId)
            return Result.Failure<IncidentDto>("Incident not found.");

        incident.Assign(request.AssignToUserId, request.AssignVehicleId);
        incident.SetUpdatedBy(request.DispatcherId.ToString());

        if (request.AssignVehicleId.HasValue)
        {
            var vehicle = await _uow.Vehicles.GetByIdAsync(request.AssignVehicleId.Value, ct);
            vehicle?.UpdateStatus(VehicleStatus.Dispatched);
            if (vehicle is not null) await _uow.Vehicles.UpdateAsync(vehicle, ct);

            // Create dispatch assignment record
            var assignment = DispatchAssignment.Create(
                request.TenantId, request.AssignVehicleId.Value,
                request.IncidentId, request.AssignToUserId, request.DispatcherId,
                incident.Priority
            );
            await _uow.Dispatches.AddAsync(assignment, ct);
        }

        await _uow.Incidents.UpdateAsync(incident, ct);
        await _uow.SaveChangesAsync(ct);

        await _signalR.SendDispatchUpdateAsync(request.TenantId, new
        {
            type = "incident_assigned",
            incidentId = incident.Id,
            vehicleId = request.AssignVehicleId,
            userId = request.AssignToUserId
        }, ct);

        return Result.Success<IncidentDto>(null!); // simplified — full impl loads related data
    }
}

public class GetIncidentsHandler : IRequestHandler<GetIncidentsQuery, PagedResult<IncidentListDto>>
{
    private readonly IUnitOfWork _uow;

    public GetIncidentsHandler(IUnitOfWork uow) => _uow = uow;

    public async Task<PagedResult<IncidentListDto>> Handle(GetIncidentsQuery request, CancellationToken ct)
    {
        var incidents = await _uow.Incidents.GetByTenantAsync(request.TenantId, request.Status, ct);

        var filtered = incidents.AsQueryable();

        if (request.Priority.HasValue)
            filtered = filtered.Where(i => i.Priority == request.Priority.Value);

        if (request.Category.HasValue)
            filtered = filtered.Where(i => i.Category == request.Category.Value);

        if (request.AssignedToId.HasValue)
            filtered = filtered.Where(i => i.AssignedToId == request.AssignedToId.Value);

        if (request.From.HasValue)
            filtered = filtered.Where(i => i.OpenedAt >= request.From.Value);

        if (request.To.HasValue)
            filtered = filtered.Where(i => i.OpenedAt <= request.To.Value);

        if (!string.IsNullOrEmpty(request.Paged.Search))
        {
            var search = request.Paged.Search.ToLower();
            filtered = filtered.Where(i =>
                i.Title.ToLower().Contains(search) ||
                i.ReferenceNo.ToLower().Contains(search) ||
                (i.Address != null && i.Address.ToLower().Contains(search)));
        }

        var totalCount = filtered.Count();

        var ordered = request.Paged.SortBy switch
        {
            "priority" => request.Paged.SortDescending
                ? filtered.OrderByDescending(i => i.Priority)
                : filtered.OrderBy(i => i.Priority),
            "openedAt" => request.Paged.SortDescending
                ? filtered.OrderByDescending(i => i.OpenedAt)
                : filtered.OrderBy(i => i.OpenedAt),
            _ => filtered.OrderByDescending(i => i.OpenedAt)
        };

        var items = ordered
            .Skip(request.Paged.Skip)
            .Take(request.Paged.PageSize)
            .Select(i => new IncidentListDto(
                i.Id, i.ReferenceNo, i.Title, i.Category.ToString(),
                (int)i.Priority, i.Status.ToString(),
                i.Latitude, i.Longitude, i.Address,
                null, null, // driver/vehicle names loaded by infrastructure with joins
                i.SlaBreached, i.IsSlaAtRisk(), i.OpenedAt, i.MinutesOpen
            ));

        return PagedResult<IncidentListDto>.Create(items, totalCount, request.Paged.Page, request.Paged.PageSize);
    }
}

public class CloseIncidentHandler : IRequestHandler<CloseIncidentCommand, Result>
{
    private readonly IUnitOfWork _uow;
    private readonly ISignalRService _signalR;

    public CloseIncidentHandler(IUnitOfWork uow, ISignalRService signalR)
    {
        _uow = uow;
        _signalR = signalR;
    }

    public async Task<Result> Handle(CloseIncidentCommand request, CancellationToken ct)
    {
        var incident = await _uow.Incidents.GetByIdAsync(request.IncidentId, ct);
        if (incident is null || incident.TenantId != request.TenantId)
            return Result.Failure("Incident not found.");

        incident.UpdateStatus(IncidentStatus.Closed);
        incident.SetUpdatedBy(request.ClosedById.ToString());

        if (!string.IsNullOrWhiteSpace(request.ResolutionNote))
        {
            var update = IncidentUpdate.Create(request.TenantId, incident.Id,
                request.ClosedById, "resolution", request.ResolutionNote);
            await _uow.SaveChangesAsync(ct);
        }

        // Free the vehicle if one was assigned
        if (incident.AssignedVehicleId.HasValue)
        {
            var vehicle = await _uow.Vehicles.GetByIdAsync(incident.AssignedVehicleId.Value, ct);
            vehicle?.UpdateStatus(VehicleStatus.Available);
            if (vehicle is not null) await _uow.Vehicles.UpdateAsync(vehicle, ct);
        }

        await _uow.Incidents.UpdateAsync(incident, ct);
        await _uow.SaveChangesAsync(ct);

        await _signalR.SendToTenantAsync(request.TenantId, "IncidentClosed", new
        {
            incidentId = incident.Id,
            referenceNo = incident.ReferenceNo,
            closedAt = incident.ClosedAt
        }, ct);

        return Result.Success();
    }
}
