using CivicOps.Domain.Enums;

namespace CivicOps.Domain.Entities;

public class DispatchAssignment : TenantEntity
{
    public Guid? IncidentId { get; private set; }
    public Guid VehicleId { get; private set; }
    public Guid? DriverId { get; private set; }
    public Guid? DispatcherId { get; private set; }
    public bool AiRecommended { get; private set; }
    public decimal? AiConfidence { get; private set; }
    public string? AiReasoning { get; private set; }
    public DispatchStatus Status { get; private set; } = DispatchStatus.Pending;
    public IncidentPriority Priority { get; private set; } = IncidentPriority.Medium;

    // Route
    public decimal? OriginLat { get; private set; }
    public decimal? OriginLng { get; private set; }
    public decimal? DestLat { get; private set; }
    public decimal? DestLng { get; private set; }
    public string? OptimizedRouteJson { get; private set; }
    public decimal? EstDistanceKm { get; private set; }
    public int? EstDurationMin { get; private set; }
    public int? ActualDurationMin { get; private set; }

    // Timestamps
    public DateTime? DispatchedAt { get; private set; }
    public DateTime? AcknowledgedAt { get; private set; }
    public DateTime? ArrivedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public string? Notes { get; private set; }

    // Navigation
    public Incident? Incident { get; private set; }
    public Vehicle? Vehicle { get; private set; }
    public User? Driver { get; private set; }
    public User? Dispatcher { get; private set; }

    private DispatchAssignment() { }

    public static DispatchAssignment Create(Guid tenantId, Guid vehicleId,
        Guid? incidentId = null, Guid? driverId = null, Guid? dispatcherId = null,
        IncidentPriority priority = IncidentPriority.Medium,
        bool aiRecommended = false, decimal? aiConfidence = null, string? aiReasoning = null,
        decimal? destLat = null, decimal? destLng = null,
        decimal? estDistanceKm = null, int? estDurationMin = null)
    {
        return new DispatchAssignment
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            VehicleId = vehicleId,
            IncidentId = incidentId,
            DriverId = driverId,
            DispatcherId = dispatcherId,
            Priority = priority,
            AiRecommended = aiRecommended,
            AiConfidence = aiConfidence,
            AiReasoning = aiReasoning,
            DestLat = destLat,
            DestLng = destLng,
            EstDistanceKm = estDistanceKm,
            EstDurationMin = estDurationMin,
            DispatchedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Acknowledge()
    {
        AcknowledgedAt = DateTime.UtcNow;
        Status = DispatchStatus.Acknowledged;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkArrived()
    {
        ArrivedAt = DateTime.UtcNow;
        Status = DispatchStatus.OnScene;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Complete(string? notes = null)
    {
        CompletedAt = DateTime.UtcNow;
        Status = DispatchStatus.Completed;
        Notes = notes;
        ActualDurationMin = ArrivedAt.HasValue
            ? (int)(DateTime.UtcNow - DispatchedAt!.Value).TotalMinutes
            : null;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Cancel(string? reason = null)
    {
        Status = DispatchStatus.Cancelled;
        Notes = reason;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetOrigin(decimal lat, decimal lng)
    {
        OriginLat = lat;
        OriginLng = lng;
    }

    public void SetOptimizedRoute(string routeJson, decimal distanceKm, int durationMin)
    {
        OptimizedRouteJson = routeJson;
        EstDistanceKm = distanceKm;
        EstDurationMin = durationMin;
        UpdatedAt = DateTime.UtcNow;
    }
}
