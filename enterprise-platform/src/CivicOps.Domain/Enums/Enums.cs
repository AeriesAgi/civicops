namespace CivicOps.Domain.Enums;

public enum UserRole
{
    SuperAdmin = 0,
    OperationsManager = 1,
    Dispatcher = 2,
    Supervisor = 3,
    PatrolOfficer = 4,
    FleetManager = 5,
    Driver = 6,
    ClientViewer = 7
}

public enum VehicleType
{
    Patrol = 0,
    Response = 1,
    Command = 2,
    Logistics = 3,
    Recovery = 4,
    Escort = 5,
    Motorcycle = 6,
    Other = 99
}

public enum VehicleStatus
{
    Available = 0,
    Active = 1,
    Dispatched = 2,
    OnScene = 3,
    Idle = 4,
    Maintenance = 5,
    OutOfService = 6,
    Offline = 7
}

public enum FuelType
{
    Petrol = 0,
    Diesel = 1,
    Electric = 2,
    Hybrid = 3,
    LPG = 4
}

public enum IncidentStatus
{
    Open = 0,
    Assigned = 1,
    InProgress = 2,
    Escalated = 3,
    Resolved = 4,
    Closed = 5,
    Cancelled = 6
}

public enum IncidentPriority
{
    Critical = 1,
    High = 2,
    Medium = 3,
    Low = 4,
    Routine = 5
}

public enum IncidentSeverity
{
    Critical = 1,
    High = 2,
    Medium = 3,
    Low = 4
}

public enum IncidentCategory
{
    ArmedResponse = 1,
    AlarmActivation = 2,
    MotorVehicleAccident = 3,
    MedicalAssist = 4,
    SuspiciousActivity = 5,
    Escort = 6,
    Patrol = 7,
    Fire = 8,
    Theft = 9,
    Trespassing = 10,
    Vandalism = 11,
    Assault = 12,
    VehicleBreakdown = 13,
    Infrastructure = 14,
    Other = 99
}

public enum DispatchStatus
{
    Pending = 0,
    Dispatched = 1,
    Acknowledged = 2,
    EnRoute = 3,
    OnScene = 4,
    Completed = 5,
    Cancelled = 6
}

public enum NotificationChannel
{
    Push = 0,
    Sms = 1,
    Email = 2,
    WhatsApp = 3,
    InApp = 4
}
