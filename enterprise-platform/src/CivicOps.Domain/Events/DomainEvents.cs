using CivicOps.Domain.Enums;

namespace CivicOps.Domain.Events;

public interface IDomainEvent
{
    Guid EventId { get; }
    DateTime OccurredAt { get; }
}

public abstract class DomainEvent : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}

public class IncidentCreatedEvent : DomainEvent
{
    public Guid IncidentId { get; }
    public Guid TenantId { get; }
    public IncidentPriority Priority { get; }

    public IncidentCreatedEvent(Guid incidentId, Guid tenantId, IncidentPriority priority)
    {
        IncidentId = incidentId;
        TenantId = tenantId;
        Priority = priority;
    }
}

public class IncidentEscalatedEvent : DomainEvent
{
    public Guid IncidentId { get; }
    public Guid TenantId { get; }
    public Guid EscalatedToId { get; }

    public IncidentEscalatedEvent(Guid incidentId, Guid tenantId, Guid escalatedToId)
    {
        IncidentId = incidentId;
        TenantId = tenantId;
        EscalatedToId = escalatedToId;
    }
}

public class SlaBreachedEvent : DomainEvent
{
    public Guid IncidentId { get; }
    public Guid TenantId { get; }

    public SlaBreachedEvent(Guid incidentId, Guid tenantId)
    {
        IncidentId = incidentId;
        TenantId = tenantId;
    }
}

public class PanicTriggeredEvent : DomainEvent
{
    public Guid PanicEventId { get; }
    public Guid TenantId { get; }
    public Guid UserId { get; }
    public decimal Latitude { get; }
    public decimal Longitude { get; }

    public PanicTriggeredEvent(Guid panicEventId, Guid tenantId, Guid userId,
        decimal latitude, decimal longitude)
    {
        PanicEventId = panicEventId;
        TenantId = tenantId;
        UserId = userId;
        Latitude = latitude;
        Longitude = longitude;
    }
}

public class GeofenceBreachedEvent : DomainEvent
{
    public Guid VehicleId { get; }
    public Guid TenantId { get; }
    public Guid GeofenceId { get; }
    public string GeofenceName { get; }
    public string EventType { get; } // "enter" | "exit"
    public decimal Latitude { get; }
    public decimal Longitude { get; }

    public GeofenceBreachedEvent(Guid vehicleId, Guid tenantId, Guid geofenceId,
        string geofenceName, string eventType, decimal lat, decimal lng)
    {
        VehicleId = vehicleId;
        TenantId = tenantId;
        GeofenceId = geofenceId;
        GeofenceName = geofenceName;
        EventType = eventType;
        Latitude = lat;
        Longitude = lng;
    }
}

public class VehicleDispatchedEvent : DomainEvent
{
    public Guid AssignmentId { get; }
    public Guid VehicleId { get; }
    public Guid TenantId { get; }
    public Guid? IncidentId { get; }

    public VehicleDispatchedEvent(Guid assignmentId, Guid vehicleId, Guid tenantId, Guid? incidentId)
    {
        AssignmentId = assignmentId;
        VehicleId = vehicleId;
        TenantId = tenantId;
        IncidentId = incidentId;
    }
}

public class MaintenanceDueEvent : DomainEvent
{
    public Guid VehicleId { get; }
    public Guid TenantId { get; }
    public string MaintenanceType { get; }
    public bool IsOverdue { get; }

    public MaintenanceDueEvent(Guid vehicleId, Guid tenantId, string maintenanceType, bool isOverdue)
    {
        VehicleId = vehicleId;
        TenantId = tenantId;
        MaintenanceType = maintenanceType;
        IsOverdue = isOverdue;
    }
}
