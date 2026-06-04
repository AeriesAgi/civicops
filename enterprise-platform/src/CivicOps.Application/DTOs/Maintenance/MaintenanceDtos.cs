using MediatR;
using CivicOps.Application.DTOs.Common;

namespace CivicOps.Application.DTOs.Maintenance;

public record MaintenanceScheduleDto(
    Guid Id,
    Guid VehicleId,
    string Registration,
    string? VehicleAlias,
    string Type,
    string? Description,
    int? IntervalKm,
    int? IntervalDays,
    decimal? LastServiceKm,
    DateTime? LastServiceDate,
    decimal? NextDueKm,
    DateTime? NextDueDate,
    string Priority,
    bool AiPredicted,
    decimal? AiConfidence,
    decimal? EstimatedCost,
    bool IsActive,
    bool IsDue,
    int? KmUntilDue,
    int? DaysUntilDue
);

public record MaintenanceRecordDto(
    Guid Id,
    Guid VehicleId,
    string Registration,
    Guid? ScheduleId,
    string Type,
    string Description,
    string? Technician,
    string? Workshop,
    decimal? Cost,
    decimal? OdometerAtService,
    List<string> Documents,
    DateTime ServicedAt,
    DateTime? NextDueDate,
    decimal? NextDueKm
);

public record MaintenanceAlertDto(
    Guid VehicleId,
    string Registration,
    string? Alias,
    string Type,
    string Priority,
    bool IsOverdue,
    int? KmOverdue,
    int? DaysOverdue,
    decimal? EstimatedCost,
    bool AiPredicted
);

public record PredictiveMaintenanceResultDto(
    Guid VehicleId,
    string Registration,
    IEnumerable<PredictionItemDto> Predictions,
    int OverallRiskScore,
    string RiskLevel,
    string AiSummary
);

public record PredictionItemDto(
    string Component,
    string PredictedFailureType,
    DateTime? EstimatedFailureDate,
    decimal? EstimatedFailureKm,
    decimal Confidence,
    string Urgency,
    string Recommendation
);

public record CreateMaintenanceScheduleRequest(
    Guid VehicleId,
    string Type,
    string? Description = null,
    int? IntervalKm = null,
    int? IntervalDays = null,
    decimal? LastServiceKm = null,
    DateTime? LastServiceDate = null,
    decimal? EstimatedCost = null
);

public record LogMaintenanceRecordRequest(
    Guid VehicleId,
    string Type,
    string Description,
    DateTime ServicedAt,
    Guid? ScheduleId = null,
    string? Technician = null,
    string? Workshop = null,
    decimal? Cost = null,
    decimal? OdometerAtService = null,
    DateTime? NextDueDate = null,
    decimal? NextDueKm = null
);

namespace CivicOps.Application.Commands.Maintenance;

using CivicOps.Application.DTOs.Maintenance;

public record CreateMaintenanceScheduleCommand(Guid TenantId, Guid CreatedById, CreateMaintenanceScheduleRequest Request)
    : IRequest<Result<MaintenanceScheduleDto>>;

public record LogMaintenanceRecordCommand(Guid TenantId, Guid LoggedById, LogMaintenanceRecordRequest Request)
    : IRequest<Result<MaintenanceRecordDto>>;

public record RunPredictiveAnalysisCommand(Guid TenantId, Guid VehicleId)
    : IRequest<Result<PredictiveMaintenanceResultDto>>;

public record UpdateScheduleCommand(Guid TenantId, Guid ScheduleId, CreateMaintenanceScheduleRequest Request)
    : IRequest<Result<MaintenanceScheduleDto>>;

namespace CivicOps.Application.Queries.Maintenance;

using CivicOps.Application.DTOs.Maintenance;

public record GetMaintenanceSchedulesQuery(Guid TenantId, Guid? VehicleId = null, bool? DueOnly = null)
    : IRequest<IEnumerable<MaintenanceScheduleDto>>;

public record GetMaintenanceRecordsQuery(Guid TenantId, Guid VehicleId, PagedQuery Paged)
    : IRequest<PagedResult<MaintenanceRecordDto>>;

public record GetMaintenanceAlertsQuery(Guid TenantId)
    : IRequest<IEnumerable<MaintenanceAlertDto>>;
