using CivicOps.Domain.Enums;
using MediatR;
using CivicOps.Application.DTOs.Common;

namespace CivicOps.Application.DTOs.Incidents;

public record IncidentDto(
    Guid Id,
    Guid TenantId,
    string ReferenceNo,
    string Title,
    string? Description,
    string Category,
    string? SubCategory,
    int Priority,
    string Severity,
    string Status,
    decimal? Latitude,
    decimal? Longitude,
    string? Address,
    Guid? RegionId,
    Guid? ReportedById,
    string? ReportedByName,
    Guid? AssignedToId,
    string? AssignedToName,
    Guid? AssignedVehicleId,
    string? AssignedVehicleReg,
    Guid? EscalatedToId,
    string? EscalatedToName,
    int? SlaTargetMinutes,
    bool SlaBreached,
    bool SlaAtRisk,
    int? ResponseTimeMin,
    int? ResolutionTimeMin,
    string? AiSummary,
    decimal? AiPriorityScore,
    List<string> Tags,
    DateTime OpenedAt,
    DateTime? FirstResponseAt,
    DateTime? ResolvedAt,
    DateTime? ClosedAt,
    int MinutesOpen,
    IEnumerable<IncidentUpdateDto> RecentUpdates
);

public record IncidentListDto(
    Guid Id,
    string ReferenceNo,
    string Title,
    string Category,
    int Priority,
    string Status,
    decimal? Latitude,
    decimal? Longitude,
    string? Address,
    string? AssignedToName,
    string? AssignedVehicleReg,
    bool SlaBreached,
    bool SlaAtRisk,
    DateTime OpenedAt,
    int MinutesOpen
);

public record IncidentUpdateDto(
    Guid Id,
    Guid AuthorId,
    string AuthorName,
    string Type,
    string? Note,
    List<string> MediaUrls,
    bool IsInternal,
    DateTime CreatedAt
);

public record IncidentTimelineDto(
    Guid IncidentId,
    string ReferenceNo,
    IEnumerable<TimelineEventDto> Events
);

public record TimelineEventDto(
    DateTime At,
    string Type,
    string Description,
    string? ActorName,
    string? Data
);

public record CreateIncidentRequest(
    string Title,
    IncidentCategory Category,
    IncidentPriority Priority,
    string? Description = null,
    string? SubCategory = null,
    decimal? Latitude = null,
    decimal? Longitude = null,
    string? Address = null,
    Guid? RegionId = null,
    List<string>? Tags = null
);

public record UpdateIncidentRequest(
    string? Title,
    string? Description,
    IncidentCategory? Category,
    IncidentPriority? Priority,
    IncidentSeverity? Severity,
    string? Address,
    List<string>? Tags
);

public record AssignIncidentRequest(Guid? UserId, Guid? VehicleId);
public record EscalateIncidentRequest(Guid EscalateToUserId, string? Reason);
public record CloseIncidentRequest(string? ResolutionNote);
public record AddIncidentUpdateRequest(string Type, string? Note, bool IsInternal = false);

namespace CivicOps.Application.Commands.Incidents;

using CivicOps.Application.DTOs.Incidents;

public record CreateIncidentCommand(Guid TenantId, Guid CreatedById, CreateIncidentRequest Request)
    : IRequest<Result<IncidentDto>>;

public record UpdateIncidentCommand(Guid TenantId, Guid IncidentId, Guid UpdatedById, UpdateIncidentRequest Request)
    : IRequest<Result<IncidentDto>>;

public record AssignIncidentCommand(Guid TenantId, Guid IncidentId, Guid DispatcherId,
    Guid? AssignToUserId, Guid? AssignVehicleId)
    : IRequest<Result<IncidentDto>>;

public record EscalateIncidentCommand(Guid TenantId, Guid IncidentId, Guid EscalatedById,
    Guid EscalateToUserId, string? Reason)
    : IRequest<Result<IncidentDto>>;

public record CloseIncidentCommand(Guid TenantId, Guid IncidentId, Guid ClosedById, string? ResolutionNote)
    : IRequest<Result>;

public record AddIncidentUpdateCommand(Guid TenantId, Guid IncidentId, Guid AuthorId,
    string Type, string? Note, bool IsInternal, List<string>? MediaUrls)
    : IRequest<Result<IncidentUpdateDto>>;

public record UploadIncidentMediaCommand(Guid TenantId, Guid IncidentId, Guid UploaderId,
    Stream FileStream, string FileName, string ContentType)
    : IRequest<Result<string>>;

namespace CivicOps.Application.Queries.Incidents;

using CivicOps.Application.DTOs.Incidents;

public record GetIncidentsQuery(
    Guid TenantId,
    PagedQuery Paged,
    IncidentStatus? Status = null,
    IncidentPriority? Priority = null,
    IncidentCategory? Category = null,
    Guid? AssignedToId = null,
    DateTime? From = null,
    DateTime? To = null
) : IRequest<PagedResult<IncidentListDto>>;

public record GetIncidentByIdQuery(Guid TenantId, Guid IncidentId)
    : IRequest<Result<IncidentDto>>;

public record GetIncidentTimelineQuery(Guid TenantId, Guid IncidentId)
    : IRequest<Result<IncidentTimelineDto>>;

public record GetOpenIncidentsQuery(Guid TenantId)
    : IRequest<IEnumerable<IncidentListDto>>;

public record GetSlaAtRiskIncidentsQuery(Guid TenantId)
    : IRequest<IEnumerable<IncidentListDto>>;
