using CivicOps.Domain.Enums;
using MediatR;
using CivicOps.Application.DTOs.Common;

namespace CivicOps.Application.DTOs.Dispatch;

public record DispatchAssignmentDto(
    Guid Id,
    Guid TenantId,
    Guid? IncidentId,
    string? IncidentReferenceNo,
    Guid VehicleId,
    string VehicleRegistration,
    string? VehicleAlias,
    Guid? DriverId,
    string? DriverName,
    Guid? DispatcherId,
    string? DispatcherName,
    bool AiRecommended,
    decimal? AiConfidence,
    string? AiReasoning,
    string Status,
    int Priority,
    decimal? OriginLat,
    decimal? OriginLng,
    decimal? DestLat,
    decimal? DestLng,
    decimal? EstDistanceKm,
    int? EstDurationMin,
    int? ActualDurationMin,
    DateTime? DispatchedAt,
    DateTime? AcknowledgedAt,
    DateTime? ArrivedAt,
    DateTime? CompletedAt,
    string? Notes
);

public record DispatchQueueItemDto(
    Guid AssignmentId,
    Guid? IncidentId,
    string? IncidentRef,
    string? IncidentTitle,
    int Priority,
    string Status,
    string VehicleRegistration,
    string? DriverName,
    int? EstDurationMin,
    decimal? EstDistanceKm,
    DateTime DispatchedAt,
    bool AiRecommended
);

public record DispatchRecommendationDto(
    Guid VehicleId,
    string Registration,
    string? Alias,
    string? DriverName,
    decimal DistanceKm,
    int EstEtaMinutes,
    decimal CurrentLat,
    decimal CurrentLng,
    decimal AiConfidence,
    string AiReasoning,
    string? AlternativeVehicleId,
    string? RouteNotes
);

public record NearestUnitRequest(
    decimal IncidentLatitude,
    decimal IncidentLongitude,
    IncidentPriority Priority,
    IncidentCategory? Category = null,
    int MaxResults = 3
);

public record CreateAssignmentRequest(
    Guid VehicleId,
    Guid? IncidentId,
    Guid? DriverId,
    int Priority = 3,
    decimal? DestLat = null,
    decimal? DestLng = null,
    string? Notes = null
);

namespace CivicOps.Application.Commands.Dispatch;

using CivicOps.Application.DTOs.Dispatch;

public record GetDispatchRecommendationCommand(Guid TenantId, NearestUnitRequest Request)
    : IRequest<Result<IEnumerable<DispatchRecommendationDto>>>;

public record CreateAssignmentCommand(Guid TenantId, Guid DispatcherId, CreateAssignmentRequest Request)
    : IRequest<Result<DispatchAssignmentDto>>;

public record AcknowledgeDispatchCommand(Guid TenantId, Guid AssignmentId, Guid UserId)
    : IRequest<Result>;

public record MarkArrivedCommand(Guid TenantId, Guid AssignmentId, Guid UserId)
    : IRequest<Result>;

public record CompleteAssignmentCommand(Guid TenantId, Guid AssignmentId, Guid UserId, string? Notes)
    : IRequest<Result>;

public record CancelAssignmentCommand(Guid TenantId, Guid AssignmentId, Guid UserId, string? Reason)
    : IRequest<Result>;

namespace CivicOps.Application.Queries.Dispatch;

using CivicOps.Application.DTOs.Dispatch;

public record GetDispatchQueueQuery(Guid TenantId)
    : IRequest<IEnumerable<DispatchQueueItemDto>>;

public record GetAssignmentByIdQuery(Guid TenantId, Guid AssignmentId)
    : IRequest<Result<DispatchAssignmentDto>>;

public record GetDispatchHistoryQuery(Guid TenantId, PagedQuery Paged, DateTime? From = null, DateTime? To = null)
    : IRequest<PagedResult<DispatchAssignmentDto>>;
