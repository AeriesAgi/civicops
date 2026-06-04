using CivicOps.Domain.Enums;
using CivicOps.Domain.Events;

namespace CivicOps.Domain.Entities;

public class Incident : TenantEntity
{
    public string ReferenceNo { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public IncidentCategory Category { get; private set; }
    public string? SubCategory { get; private set; }
    public IncidentPriority Priority { get; private set; } = IncidentPriority.Medium;
    public IncidentSeverity Severity { get; private set; } = IncidentSeverity.Medium;
    public IncidentStatus Status { get; private set; } = IncidentStatus.Open;

    // Location
    public decimal? Latitude { get; private set; }
    public decimal? Longitude { get; private set; }
    public string? Address { get; private set; }
    public Guid? RegionId { get; private set; }

    // Assignment
    public Guid? ReportedById { get; private set; }
    public Guid? AssignedToId { get; private set; }
    public Guid? AssignedVehicleId { get; private set; }
    public Guid? EscalatedToId { get; private set; }

    // SLA
    public int? SlaTargetMinutes { get; private set; }
    public bool SlaBreached { get; private set; } = false;
    public int? ResponseTimeMin { get; private set; }
    public int? ResolutionTimeMin { get; private set; }

    // AI
    public string? AiSummary { get; private set; }
    public decimal? AiPriorityScore { get; private set; }

    // Tags
    public List<string> Tags { get; private set; } = new();

    // Timestamps
    public DateTime OpenedAt { get; private set; } = DateTime.UtcNow;
    public DateTime? FirstResponseAt { get; private set; }
    public DateTime? ResolvedAt { get; private set; }
    public DateTime? ClosedAt { get; private set; }

    // Navigation
    public Tenant? Tenant { get; private set; }
    public User? ReportedBy { get; private set; }
    public User? AssignedTo { get; private set; }
    public Vehicle? AssignedVehicle { get; private set; }
    public User? EscalatedTo { get; private set; }
    public Region? Region { get; private set; }
    public ICollection<IncidentUpdate> Updates { get; private set; } = new List<IncidentUpdate>();
    public ICollection<IncidentMedia> Media { get; private set; } = new List<IncidentMedia>();
    public ICollection<DispatchAssignment> Assignments { get; private set; } = new List<DispatchAssignment>();

    // Domain events
    private readonly List<IDomainEvent> _domainEvents = new();
    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();
    public void ClearDomainEvents() => _domainEvents.Clear();

    private Incident() { }

    public static Incident Create(Guid tenantId, string referenceNo, string title,
        IncidentCategory category, IncidentPriority priority,
        Guid? reportedById = null, decimal? latitude = null, decimal? longitude = null,
        string? address = null, string? description = null)
    {
        var incident = new Incident
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ReferenceNo = referenceNo,
            Title = title.Trim(),
            Description = description,
            Category = category,
            Priority = priority,
            ReportedById = reportedById,
            Latitude = latitude,
            Longitude = longitude,
            Address = address,
            OpenedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };

        incident._domainEvents.Add(new IncidentCreatedEvent(incident.Id, tenantId, priority));
        return incident;
    }

    public void Assign(Guid? userId, Guid? vehicleId)
    {
        AssignedToId = userId;
        AssignedVehicleId = vehicleId;
        Status = IncidentStatus.Assigned;

        if (!FirstResponseAt.HasValue)
        {
            FirstResponseAt = DateTime.UtcNow;
            ResponseTimeMin = (int)(DateTime.UtcNow - OpenedAt).TotalMinutes;
        }

        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateStatus(IncidentStatus status)
    {
        var previous = Status;
        Status = status;

        if (status == IncidentStatus.Resolved && !ResolvedAt.HasValue)
        {
            ResolvedAt = DateTime.UtcNow;
            ResolutionTimeMin = (int)(DateTime.UtcNow - OpenedAt).TotalMinutes;
        }

        if (status == IncidentStatus.Closed)
            ClosedAt = DateTime.UtcNow;

        UpdatedAt = DateTime.UtcNow;
    }

    public void Escalate(Guid escalatedToId)
    {
        EscalatedToId = escalatedToId;
        Status = IncidentStatus.Escalated;
        UpdatedAt = DateTime.UtcNow;
        _domainEvents.Add(new IncidentEscalatedEvent(Id, TenantId, escalatedToId));
    }

    public void UpdatePriority(IncidentPriority priority, IncidentSeverity severity)
    {
        Priority = priority;
        Severity = severity;
        UpdatedAt = DateTime.UtcNow;
    }

    public void AddTag(string tag)
    {
        if (!Tags.Contains(tag, StringComparer.OrdinalIgnoreCase))
            Tags.Add(tag.ToUpperInvariant());
    }

    public void SetAiAnalysis(string? summary, decimal? priorityScore)
    {
        AiSummary = summary;
        AiPriorityScore = priorityScore;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetSlaTarget(int minutes)
    {
        SlaTargetMinutes = minutes;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkSlaBreach()
    {
        if (!SlaBreached)
        {
            SlaBreached = true;
            UpdatedAt = DateTime.UtcNow;
            _domainEvents.Add(new SlaBreachedEvent(Id, TenantId));
        }
    }

    public bool IsSlaAtRisk()
    {
        if (!SlaTargetMinutes.HasValue || Status == IncidentStatus.Closed) return false;
        var elapsed = (DateTime.UtcNow - OpenedAt).TotalMinutes;
        return elapsed >= SlaTargetMinutes.Value * 0.8;
    }

    public int MinutesOpen => (int)(DateTime.UtcNow - OpenedAt).TotalMinutes;
}
