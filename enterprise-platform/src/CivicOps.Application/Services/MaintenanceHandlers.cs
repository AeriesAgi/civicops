using CivicOps.Application.Commands.Maintenance;
using CivicOps.Application.DTOs.Common;
using CivicOps.Application.DTOs.Maintenance;
using CivicOps.Application.Interfaces;
using CivicOps.Application.Queries.Maintenance;
using CivicOps.Domain.Entities;
using CivicOps.Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace CivicOps.Application.Services;

public class CreateMaintenanceScheduleHandler
    : IRequestHandler<CreateMaintenanceScheduleCommand, Result<MaintenanceScheduleDto>>
{
    private readonly IUnitOfWork _uow;

    public CreateMaintenanceScheduleHandler(IUnitOfWork uow) => _uow = uow;

    public async Task<Result<MaintenanceScheduleDto>> Handle(
        CreateMaintenanceScheduleCommand request, CancellationToken ct)
    {
        var vehicle = await _uow.Vehicles.GetByIdAsync(request.Request.VehicleId, ct);
        if (vehicle is null || vehicle.TenantId != request.TenantId)
            return Result.Failure<MaintenanceScheduleDto>("Vehicle not found.");

        var schedule = MaintenanceSchedule.Create(
            request.TenantId,
            request.Request.VehicleId,
            request.Request.Type,
            request.Request.Description,
            request.Request.IntervalKm,
            request.Request.IntervalDays,
            request.Request.EstimatedCost
        );

        schedule.SetCreatedBy(request.CreatedById.ToString());

        await _uow.Maintenance.AddAsync(schedule, ct);
        await _uow.SaveChangesAsync(ct);

        return Result.Success(MapScheduleDto(schedule, vehicle));
    }
}

public class LogMaintenanceRecordHandler
    : IRequestHandler<LogMaintenanceRecordCommand, Result<MaintenanceRecordDto>>
{
    private readonly IUnitOfWork _uow;

    public LogMaintenanceRecordHandler(IUnitOfWork uow) => _uow = uow;

    public async Task<Result<MaintenanceRecordDto>> Handle(
        LogMaintenanceRecordCommand request, CancellationToken ct)
    {
        var vehicle = await _uow.Vehicles.GetByIdAsync(request.Request.VehicleId, ct);
        if (vehicle is null || vehicle.TenantId != request.TenantId)
            return Result.Failure<MaintenanceRecordDto>("Vehicle not found.");

        var record = MaintenanceRecord.Create(
            request.TenantId,
            request.Request.VehicleId,
            request.Request.Type,
            request.Request.Description,
            request.Request.ServicedAt,
            request.Request.ScheduleId,
            request.Request.Technician,
            request.Request.Workshop,
            request.Request.Cost,
            request.Request.OdometerAtService
        );

        // Update related schedule if provided
        if (request.Request.ScheduleId.HasValue)
        {
            var schedule = await _uow.Maintenance.GetByIdAsync(request.Request.ScheduleId.Value, ct);
            if (schedule is not null)
            {
                // Update next due based on interval
                // (simplified — real impl would use reflection/pattern matching on schedule type)
            }
        }

        // Update vehicle health score
        var newHealth = Math.Min(100, vehicle.HealthScore + 10);
        vehicle.UpdateHealthScore(newHealth);

        await _uow.Maintenance.AddRecordAsync(record, ct);
        await _uow.Vehicles.UpdateAsync(vehicle, ct);
        await _uow.SaveChangesAsync(ct);

        return Result.Success(MapRecordDto(record, vehicle));
    }
}

public class GetMaintenanceAlertsHandler
    : IRequestHandler<GetMaintenanceAlertsQuery, IEnumerable<MaintenanceAlertDto>>
{
    private readonly IUnitOfWork _uow;

    public GetMaintenanceAlertsHandler(IUnitOfWork uow) => _uow = uow;

    public async Task<IEnumerable<MaintenanceAlertDto>> Handle(
        GetMaintenanceAlertsQuery request, CancellationToken ct)
    {
        var schedules = await _uow.Maintenance.GetDueSchedulesAsync(request.TenantId, ct);
        var alerts = new List<MaintenanceAlertDto>();

        foreach (var s in schedules)
        {
            var vehicle = await _uow.Vehicles.GetByIdAsync(s.VehicleId, ct);
            if (vehicle is null) continue;

            var kmOverdue = s.NextDueKm.HasValue
                ? (int)(vehicle.OdometerKm - s.NextDueKm.Value)
                : (int?)null;

            var daysOverdue = s.NextDueDate.HasValue
                ? (int)(DateTime.UtcNow - s.NextDueDate.Value).TotalDays
                : (int?)null;

            var isOverdue = (kmOverdue.HasValue && kmOverdue > 0)
                         || (daysOverdue.HasValue && daysOverdue > 0);

            alerts.Add(new MaintenanceAlertDto(
                vehicle.Id, vehicle.Registration, vehicle.Alias,
                s.Type, s.Priority, isOverdue,
                kmOverdue > 0 ? kmOverdue : null,
                daysOverdue > 0 ? daysOverdue : null,
                s.EstimatedCost, s.AiPredicted
            ));
        }

        return alerts.OrderByDescending(a => a.IsOverdue).ThenBy(a => a.Priority);
    }
}

/// <summary>
/// AI-powered predictive maintenance analysis.
/// Analyses vehicle history, odometer, GPS patterns, and maintenance records
/// to predict upcoming failures and recommend preventive action.
/// </summary>
public class RunPredictiveAnalysisHandler
    : IRequestHandler<RunPredictiveAnalysisCommand, Result<PredictiveMaintenanceResultDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ILLMProvider _llm;
    private readonly ILogger<RunPredictiveAnalysisHandler> _logger;

    private const string PredictiveSystemPrompt = """
        You are a fleet predictive maintenance AI for an operational vehicle fleet.
        Analyse the vehicle data and predict maintenance needs.

        Return ONLY valid JSON (no markdown):
        {
          "predictions": [
            {
              "component": "Engine Oil",
              "predictedFailureType": "Degradation",
              "estimatedFailureDays": 14,
              "confidence": 0.87,
              "urgency": "high",
              "recommendation": "Schedule oil change within 7 days"
            }
          ],
          "overallRiskScore": 0-100,
          "riskLevel": "critical|high|medium|low",
          "summary": "2-3 sentence fleet health assessment"
        }
        """;

    public RunPredictiveAnalysisHandler(IUnitOfWork uow, ILLMProvider llm,
        ILogger<RunPredictiveAnalysisHandler> logger)
    {
        _uow = uow;
        _llm = llm;
        _logger = logger;
    }

    public async Task<Result<PredictiveMaintenanceResultDto>> Handle(
        RunPredictiveAnalysisCommand request, CancellationToken ct)
    {
        var vehicle = await _uow.Vehicles.GetByIdAsync(request.VehicleId, ct);
        if (vehicle is null || vehicle.TenantId != request.TenantId)
            return Result.Failure<PredictiveMaintenanceResultDto>("Vehicle not found.");

        var records = (await _uow.Maintenance.GetRecordsByVehicleAsync(request.VehicleId, 20, ct)).ToList();
        var schedules = (await _uow.Maintenance.GetByVehicleAsync(request.VehicleId, ct)).ToList();

        // Build context for AI
        var vehicleContext = new
        {
            registration = vehicle.Registration,
            make = vehicle.Make,
            model = vehicle.Model,
            year = vehicle.Year,
            odometerKm = vehicle.OdometerKm,
            healthScore = vehicle.HealthScore,
            fuelType = vehicle.FuelType.ToString(),
            lastGpsAt = vehicle.LastGpsAt,
            lastSpeedKmh = vehicle.LastSpeedKmh
        };

        var maintenanceHistory = records.Take(10).Select(r => new
        {
            type = r.Type,
            servicedAt = r.ServicedAt.ToString("yyyy-MM-dd"),
            odometerAtService = r.OdometerAtService,
            cost = r.Cost
        });

        var activeSchedules = schedules.Select(s => new
        {
            type = s.Type,
            intervalKm = s.IntervalKm,
            lastServiceKm = s.LastServiceKm,
            nextDueKm = s.NextDueKm,
            nextDueDate = s.NextDueDate?.ToString("yyyy-MM-dd"),
            isDue = s.IsDue(vehicle.OdometerKm)
        });

        var userPrompt = $"""
            VEHICLE:
            {JsonSerializer.Serialize(vehicleContext)}

            MAINTENANCE HISTORY (last 10 records):
            {JsonSerializer.Serialize(maintenanceHistory)}

            ACTIVE SCHEDULES:
            {JsonSerializer.Serialize(activeSchedules)}

            Provide predictive maintenance analysis for this vehicle.
            """;

        if (!_llm.IsAvailable)
        {
            // Fallback: rule-based analysis
            return Result.Success(GenerateRuleBasedAnalysis(vehicle, schedules));
        }

        try
        {
            var aiResponse = await _llm.CompleteAsync(PredictiveSystemPrompt, userPrompt, ct);
            var parsed = ParsePredictiveResponse(aiResponse, vehicle);
            return Result.Success(parsed);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Predictive maintenance AI failed for vehicle {Id}", vehicle.Id);
            return Result.Success(GenerateRuleBasedAnalysis(vehicle, schedules));
        }
    }

    private static PredictiveMaintenanceResultDto GenerateRuleBasedAnalysis(
        Vehicle vehicle, List<MaintenanceSchedule> schedules)
    {
        var predictions = new List<PredictionItemDto>();
        int riskScore = 100 - vehicle.HealthScore;

        foreach (var schedule in schedules.Where(s => s.IsDue(vehicle.OdometerKm)))
        {
            predictions.Add(new PredictionItemDto(
                Component: schedule.Type,
                PredictedFailureType: "Scheduled interval exceeded",
                EstimatedFailureDate: schedule.NextDueDate,
                EstimatedFailureKm: schedule.NextDueKm,
                Confidence: 0.95m,
                Urgency: "high",
                Recommendation: $"Schedule {schedule.Type} immediately"
            ));
            riskScore = Math.Min(100, riskScore + 20);
        }

        var riskLevel = riskScore >= 70 ? "high" : riskScore >= 40 ? "medium" : "low";

        return new PredictiveMaintenanceResultDto(
            vehicle.Id, vehicle.Registration, predictions,
            riskScore, riskLevel,
            $"Vehicle {vehicle.Registration} has {predictions.Count} maintenance items due. " +
            $"Current health score: {vehicle.HealthScore}/100."
        );
    }

    private static PredictiveMaintenanceResultDto ParsePredictiveResponse(string response, Vehicle vehicle)
    {
        try
        {
            var cleaned = response.Trim().TrimStart('`').TrimEnd('`');
            if (cleaned.StartsWith("json")) cleaned = cleaned[4..].Trim();

            using var doc = JsonDocument.Parse(cleaned);
            var root = doc.RootElement;

            var predictions = new List<PredictionItemDto>();
            if (root.TryGetProperty("predictions", out var preds))
            {
                foreach (var p in preds.EnumerateArray())
                {
                    predictions.Add(new PredictionItemDto(
                        Component: p.TryGetProperty("component", out var c) ? c.GetString()! : "Unknown",
                        PredictedFailureType: p.TryGetProperty("predictedFailureType", out var pft)
                            ? pft.GetString()! : "Unknown",
                        EstimatedFailureDate: p.TryGetProperty("estimatedFailureDays", out var d)
                            ? DateTime.UtcNow.AddDays(d.GetInt32()) : null,
                        EstimatedFailureKm: null,
                        Confidence: p.TryGetProperty("confidence", out var conf)
                            ? (decimal)conf.GetDouble() : 0.7m,
                        Urgency: p.TryGetProperty("urgency", out var u) ? u.GetString()! : "medium",
                        Recommendation: p.TryGetProperty("recommendation", out var rec)
                            ? rec.GetString()! : ""
                    ));
                }
            }

            var riskScore = root.TryGetProperty("overallRiskScore", out var rs) ? rs.GetInt32() : 50;
            var riskLevel = root.TryGetProperty("riskLevel", out var rl) ? rl.GetString()! : "medium";
            var summary = root.TryGetProperty("summary", out var s) ? s.GetString()! : "";

            return new PredictiveMaintenanceResultDto(
                vehicle.Id, vehicle.Registration, predictions, riskScore, riskLevel, summary);
        }
        catch
        {
            return GenerateRuleBasedAnalysis(vehicle, new List<MaintenanceSchedule>());
        }
    }
}

// Mappers
file static class MaintenanceMappers
{
    internal static MaintenanceScheduleDto MapScheduleDto(MaintenanceSchedule s, Vehicle v)
    {
        var kmUntilDue = s.NextDueKm.HasValue
            ? (int?)(s.NextDueKm.Value - v.OdometerKm)
            : null;
        var daysUntilDue = s.NextDueDate.HasValue
            ? (int?)(s.NextDueDate.Value - DateTime.UtcNow).TotalDays
            : null;

        return new MaintenanceScheduleDto(
            s.Id, s.VehicleId, v.Registration, v.Alias,
            s.Type, s.Description, s.IntervalKm, s.IntervalDays,
            s.LastServiceKm, s.LastServiceDate, s.NextDueKm, s.NextDueDate,
            s.Priority, s.AiPredicted, s.AiConfidence, s.EstimatedCost,
            s.IsActive, s.IsDue(v.OdometerKm), kmUntilDue, daysUntilDue
        );
    }

    internal static MaintenanceRecordDto MapRecordDto(MaintenanceRecord r, Vehicle v) =>
        new(r.Id, r.VehicleId, v.Registration, r.ScheduleId, r.Type,
            r.Description, r.Technician, r.Workshop, r.Cost,
            r.OdometerAtService, r.Documents, r.ServicedAt,
            r.NextDueDate, r.NextDueKm);
}

// Make file-scoped helpers available to handlers via extension
internal static class MaintenanceMapExtensions
{
    internal static MaintenanceScheduleDto MapScheduleDto(this MaintenanceSchedule s, Vehicle v)
        => MaintenanceMappers.MapScheduleDto(s, v);

    internal static MaintenanceRecordDto MapRecordDto(this MaintenanceRecord r, Vehicle v)
        => MaintenanceMappers.MapRecordDto(r, v);
}
