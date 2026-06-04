using CivicOps.Application.Commands.Dispatch;
using CivicOps.Application.DTOs.Common;
using CivicOps.Application.DTOs.Dispatch;
using CivicOps.Application.Interfaces;
using CivicOps.Application.Queries.Dispatch;
using CivicOps.Domain.Entities;
using CivicOps.Domain.Enums;
using CivicOps.Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace CivicOps.Application.Services.AI;

/// <summary>
/// Core AI dispatch recommendation engine.
/// Combines spatial nearest-unit calculation with LLM-assisted decision reasoning.
/// Human dispatcher always makes final dispatch decision.
/// </summary>
public class DispatchRecommendationHandler
    : IRequestHandler<GetDispatchRecommendationCommand, Result<IEnumerable<DispatchRecommendationDto>>>
{
    private readonly IUnitOfWork _uow;
    private readonly ILiveFleetCache _fleetCache;
    private readonly ILLMProvider _llm;
    private readonly ILogger<DispatchRecommendationHandler> _logger;

    private const string DispatchSystemPrompt = """
        You are an AI dispatch assistant for an operational security command center.
        You assist human dispatchers — you do NOT make autonomous decisions.

        Given available units and an incident, recommend the best unit.
        Consider:
        1. Distance and ETA (primary factor)
        2. Unit type suitability for incident category
        3. Current workload and status
        4. Officer fatigue (time since last assignment)

        Respond ONLY with valid JSON (no markdown, no explanation outside JSON):
        {
          "recommendedUnitId": "<vehicle-id>",
          "confidence": 0.0-1.0,
          "reasoning": "<max 80 words, operational tone>",
          "alternativeUnitId": "<vehicle-id or null>",
          "routeNotes": "<brief routing note or null>",
          "urgencyNote": "<if critical priority, brief note>"
        }
        """;

    public DispatchRecommendationHandler(IUnitOfWork uow, ILiveFleetCache fleetCache,
        ILLMProvider llm, ILogger<DispatchRecommendationHandler> logger)
    {
        _uow = uow;
        _fleetCache = fleetCache;
        _llm = llm;
        _logger = logger;
    }

    public async Task<Result<IEnumerable<DispatchRecommendationDto>>> Handle(
        GetDispatchRecommendationCommand request, CancellationToken ct)
    {
        var req = request.Request;

        // Step 1: Get all live vehicle positions from Redis cache
        var livePositions = (await _fleetCache.GetAllPositionsAsync(request.TenantId, ct)).ToList();

        // Step 2: Filter to available/active units only
        var available = livePositions
            .Where(p => p.Status is "Available" or "Active" or "Idle")
            .ToList();

        if (!available.Any())
            return Result.Failure<IEnumerable<DispatchRecommendationDto>>(
                "No available units in live fleet cache.");

        // Step 3: Calculate distances and ETAs using Haversine
        var rankedUnits = available
            .Select(unit =>
            {
                var distKm = GeoCalculator.HaversineKm(
                    (double)req.IncidentLatitude, (double)req.IncidentLongitude,
                    (double)unit.Latitude, (double)unit.Longitude
                );

                // ETA estimate: distance / assumed avg speed (45 km/h urban)
                var etaMinutes = (int)Math.Ceiling(distKm / 45.0 * 60.0);

                return new
                {
                    Unit = unit,
                    DistanceKm = distKm,
                    EtaMinutes = etaMinutes,
                    WorkloadScore = unit.Status == "Idle" ? 0.0 : 0.5
                };
            })
            .OrderBy(u => u.EtaMinutes)
            .Take(5)
            .ToList();

        // Step 4: Build AI context prompt
        var unitsContext = rankedUnits.Select((u, i) => new
        {
            rank = i + 1,
            vehicleId = u.Unit.VehicleId.ToString(),
            registration = u.Unit.Registration,
            type = u.Unit.VehicleType,
            status = u.Unit.Status,
            distanceKm = Math.Round(u.DistanceKm, 2),
            etaMinutes = u.EtaMinutes,
            driverName = u.Unit.DriverName ?? "Unknown"
        });

        var userPrompt = $"""
            INCIDENT DETAILS:
            Priority: {req.Priority}
            Category: {req.Category?.ToString() ?? "Unknown"}
            Location: {req.IncidentLatitude}, {req.IncidentLongitude}

            AVAILABLE UNITS (ranked by proximity):
            {JsonSerializer.Serialize(unitsContext, new JsonSerializerOptions { WriteIndented = false })}

            Recommend the best unit for dispatch.
            """;

        // Step 5: Get AI recommendation
        AiRecommendationResult? aiResult = null;
        try
        {
            if (_llm.IsAvailable)
            {
                var aiResponse = await _llm.CompleteAsync(DispatchSystemPrompt, userPrompt, ct);
                aiResult = ParseAiRecommendation(aiResponse);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AI dispatch recommendation failed — falling back to distance ranking");
        }

        // Step 6: Build result DTOs — merge AI insight with spatial data
        var results = rankedUnits.Take(request.Request.MaxResults).Select(u =>
        {
            var isRecommended = aiResult?.RecommendedUnitId == u.Unit.VehicleId.ToString();
            var isAlternative = aiResult?.AlternativeUnitId == u.Unit.VehicleId.ToString();

            return new DispatchRecommendationDto(
                VehicleId: u.Unit.VehicleId,
                Registration: u.Unit.Registration,
                Alias: u.Unit.Alias,
                DriverName: u.Unit.DriverName,
                DistanceKm: (decimal)Math.Round(u.DistanceKm, 2),
                EstEtaMinutes: u.EtaMinutes,
                CurrentLat: u.Unit.Latitude,
                CurrentLng: u.Unit.Longitude,
                AiConfidence: isRecommended
                    ? (decimal)(aiResult?.Confidence ?? 0.85)
                    : isAlternative ? 0.7m : (decimal)(0.85 - (rankedUnits.IndexOf(u) * 0.1)),
                AiReasoning: isRecommended
                    ? aiResult?.Reasoning ?? $"Nearest unit at {Math.Round(u.DistanceKm, 1)} km"
                    : $"Alternative unit — ETA {u.EtaMinutes} min",
                AlternativeVehicleId: isRecommended ? aiResult?.AlternativeUnitId : null,
                RouteNotes: isRecommended ? aiResult?.RouteNotes : null
            );
        }).ToList();

        // Ensure AI-recommended unit is first
        if (aiResult?.RecommendedUnitId is not null)
        {
            var recommended = results.FirstOrDefault(r =>
                r.VehicleId.ToString() == aiResult.RecommendedUnitId);
            if (recommended is not null)
            {
                results.Remove(recommended);
                results.Insert(0, recommended);
            }
        }

        return Result.Success<IEnumerable<DispatchRecommendationDto>>(results);
    }

    private static AiRecommendationResult? ParseAiRecommendation(string response)
    {
        try
        {
            var cleaned = response.Trim();
            // Strip markdown code blocks if present
            if (cleaned.StartsWith("```"))
            {
                cleaned = cleaned.Split('\n').Skip(1).TakeWhile(l => !l.StartsWith("```"))
                    .Aggregate((a, b) => $"{a}\n{b}");
            }

            using var doc = JsonDocument.Parse(cleaned);
            var root = doc.RootElement;

            return new AiRecommendationResult(
                RecommendedUnitId: root.TryGetProperty("recommendedUnitId", out var id)
                    ? id.GetString() : null,
                Confidence: root.TryGetProperty("confidence", out var conf)
                    ? conf.GetDouble() : 0.8,
                Reasoning: root.TryGetProperty("reasoning", out var r)
                    ? r.GetString() : null,
                AlternativeUnitId: root.TryGetProperty("alternativeUnitId", out var alt)
                    ? alt.GetString() : null,
                RouteNotes: root.TryGetProperty("routeNotes", out var rn)
                    ? rn.GetString() : null
            );
        }
        catch
        {
            return null;
        }
    }

    private record AiRecommendationResult(
        string? RecommendedUnitId,
        double Confidence,
        string? Reasoning,
        string? AlternativeUnitId,
        string? RouteNotes
    );
}

public class CreateAssignmentHandler : IRequestHandler<CreateAssignmentCommand, Result<DispatchAssignmentDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ISignalRService _signalR;

    public CreateAssignmentHandler(IUnitOfWork uow, ISignalRService signalR)
    {
        _uow = uow;
        _signalR = signalR;
    }

    public async Task<Result<DispatchAssignmentDto>> Handle(CreateAssignmentCommand request, CancellationToken ct)
    {
        var vehicle = await _uow.Vehicles.GetByIdAsync(request.Request.VehicleId, ct);
        if (vehicle is null || vehicle.TenantId != request.TenantId)
            return Result.Failure<DispatchAssignmentDto>("Vehicle not found.");

        var assignment = DispatchAssignment.Create(
            request.TenantId,
            request.Request.VehicleId,
            request.Request.IncidentId,
            request.Request.DriverId ?? vehicle.AssignedDriverId,
            request.DispatcherId,
            (IncidentPriority)request.Request.Priority,
            destLat: request.Request.DestLat,
            destLng: request.Request.DestLng
        );

        if (vehicle.LastLatitude.HasValue && vehicle.LastLongitude.HasValue)
        {
            assignment.SetOrigin(vehicle.LastLatitude.Value, vehicle.LastLongitude.Value);

            if (request.Request.DestLat.HasValue && request.Request.DestLng.HasValue)
            {
                var distKm = GeoCalculator.HaversineKm(
                    (double)vehicle.LastLatitude.Value, (double)vehicle.LastLongitude.Value,
                    (double)request.Request.DestLat.Value, (double)request.Request.DestLng.Value
                );
                var etaMin = (int)Math.Ceiling(distKm / 45.0 * 60.0);
                assignment.SetOptimizedRoute("{}", (decimal)distKm, etaMin);
            }
        }

        vehicle.UpdateStatus(VehicleStatus.Dispatched);

        await _uow.Dispatches.AddAsync(assignment, ct);
        await _uow.Vehicles.UpdateAsync(vehicle, ct);
        await _uow.SaveChangesAsync(ct);

        var dto = MapToDto(assignment, vehicle);

        await _signalR.SendDispatchUpdateAsync(request.TenantId, new
        {
            type = "unit_dispatched",
            assignmentId = assignment.Id,
            vehicleId = vehicle.Id,
            incidentId = request.Request.IncidentId
        }, ct);

        return Result.Success(dto);
    }

    private static DispatchAssignmentDto MapToDto(DispatchAssignment a, Vehicle v) => new(
        a.Id, a.TenantId, a.IncidentId, null,
        a.VehicleId, v.Registration, v.Alias,
        a.DriverId, null, a.DispatcherId, null,
        a.AiRecommended, a.AiConfidence, a.AiReasoning,
        a.Status.ToString(), (int)a.Priority,
        a.OriginLat, a.OriginLng, a.DestLat, a.DestLng,
        a.EstDistanceKm, a.EstDurationMin, a.ActualDurationMin,
        a.DispatchedAt, a.AcknowledgedAt, a.ArrivedAt, a.CompletedAt,
        a.Notes
    );
}

public class AcknowledgeDispatchHandler : IRequestHandler<AcknowledgeDispatchCommand, Result>
{
    private readonly IUnitOfWork _uow;
    private readonly ISignalRService _signalR;

    public AcknowledgeDispatchHandler(IUnitOfWork uow, ISignalRService signalR)
    {
        _uow = uow;
        _signalR = signalR;
    }

    public async Task<Result> Handle(AcknowledgeDispatchCommand request, CancellationToken ct)
    {
        var assignment = await _uow.Dispatches.GetByIdAsync(request.AssignmentId, ct);
        if (assignment is null || assignment.TenantId != request.TenantId)
            return Result.Failure("Assignment not found.");

        assignment.Acknowledge();
        await _uow.Dispatches.UpdateAsync(assignment, ct);
        await _uow.SaveChangesAsync(ct);

        await _signalR.SendDispatchUpdateAsync(request.TenantId, new
        {
            type = "dispatch_acknowledged",
            assignmentId = assignment.Id,
            vehicleId = assignment.VehicleId,
            acknowledgedAt = assignment.AcknowledgedAt
        }, ct);

        return Result.Success();
    }
}

public class GetDispatchQueueHandler : IRequestHandler<GetDispatchQueueQuery, IEnumerable<DispatchQueueItemDto>>
{
    private readonly IUnitOfWork _uow;

    public GetDispatchQueueHandler(IUnitOfWork uow) => _uow = uow;

    public async Task<IEnumerable<DispatchQueueItemDto>> Handle(GetDispatchQueueQuery request, CancellationToken ct)
    {
        var assignments = await _uow.Dispatches.GetActiveByTenantAsync(request.TenantId, ct);

        return assignments
            .OrderByDescending(a => a.Priority)
            .ThenBy(a => a.DispatchedAt)
            .Select(a => new DispatchQueueItemDto(
                a.Id, a.IncidentId, null, null,
                (int)a.Priority, a.Status.ToString(),
                a.Vehicle?.Registration ?? "Unknown",
                a.Driver?.FullName,
                a.EstDurationMin, a.EstDistanceKm,
                a.DispatchedAt ?? a.CreatedAt,
                a.AiRecommended
            ));
    }
}
