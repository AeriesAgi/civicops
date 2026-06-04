using CivicOps.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace CivicOps.Api.Hubs;

/// <summary>
/// Main operational SignalR hub.
/// All authenticated users join their tenant group.
/// Role-specific groups allow targeted broadcasts.
///
/// Client events received:
///   SubscribeFleet       → joins fleet:{tenantId} group
///   SubscribeIncidents   → joins incidents:{tenantId} group
///   SubscribeDispatch    → joins dispatch:{tenantId} group
///   Ping                 → returns Pong (keep-alive verification)
///
/// Server events sent to clients:
///   GpsUpdate            → { vehicleId, lat, lng, speed, heading, status, timestamp }
///   IncidentCreated      → { incident object }
///   IncidentUpdated      → { incidentId, changes }
///   IncidentClosed       → { incidentId, closedAt }
///   DispatchUpdate       → { type, assignmentId, vehicleId, incidentId }
///   PanicTriggered       → { panicEventId, userId, lat, lng, vehicleName }
///   GeofenceAlert        → { vehicleId, registration, geofenceName, eventType }
///   SlaBreachWarning     → { incidentId, referenceNo, minutesRemaining }
///   SlaBreached          → { incidentId, referenceNo, minutesOverdue }
///   MaintenanceAlert     → { vehicleId, registration, type, priority }
///   AiRecommendation     → { type, message, entities, confidence }
///   SystemNotification   → { title, body, type, severity }
/// </summary>
[Authorize]
public class OperationsHub : Hub
{
    private readonly ILogger<OperationsHub> _logger;

    public OperationsHub(ILogger<OperationsHub> logger) => _logger = logger;

    public override async Task OnConnectedAsync()
    {
        var tenantId = GetTenantId();
        var userId = GetUserId();
        var role = GetRole();

        if (tenantId == Guid.Empty)
        {
            Context.Abort();
            return;
        }

        // Every user joins their tenant group
        await Groups.AddToGroupAsync(Context.ConnectionId, $"tenant:{tenantId}");

        // Join role-specific group
        await Groups.AddToGroupAsync(Context.ConnectionId, $"tenant:{tenantId}:role:{role}");

        // Join user-specific group for direct messages
        await Groups.AddToGroupAsync(Context.ConnectionId, $"user:{userId}");

        _logger.LogInformation("User {UserId} ({Role}) connected to ops hub [tenant:{TenantId}]",
            userId, role, tenantId);

        // Send connection acknowledgment
        await Clients.Caller.SendAsync("Connected", new
        {
            connectionId = Context.ConnectionId,
            tenantId,
            userId,
            role,
            serverTime = DateTime.UtcNow
        });

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var tenantId = GetTenantId();
        var userId = GetUserId();

        _logger.LogDebug("User {UserId} disconnected from ops hub [tenant:{TenantId}]", userId, tenantId);

        if (exception is not null)
            _logger.LogWarning(exception, "SignalR disconnected with error for user {UserId}", userId);

        await base.OnDisconnectedAsync(exception);
    }

    [HubMethodName("SubscribeFleet")]
    public async Task SubscribeToFleet()
    {
        var tenantId = GetTenantId();
        await Groups.AddToGroupAsync(Context.ConnectionId, $"fleet:{tenantId}");
        _logger.LogDebug("User {UserId} subscribed to fleet feed", GetUserId());
    }

    [HubMethodName("UnsubscribeFleet")]
    public async Task UnsubscribeFromFleet()
    {
        var tenantId = GetTenantId();
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"fleet:{tenantId}");
    }

    [HubMethodName("SubscribeIncidents")]
    public async Task SubscribeToIncidents()
    {
        var tenantId = GetTenantId();
        await Groups.AddToGroupAsync(Context.ConnectionId, $"incidents:{tenantId}");
    }

    [HubMethodName("SubscribeDispatch")]
    public async Task SubscribeToDispatch()
    {
        var tenantId = GetTenantId();
        await Groups.AddToGroupAsync(Context.ConnectionId, $"dispatch:{tenantId}");
    }

    [HubMethodName("Ping")]
    public Task<string> Ping() => Task.FromResult("Pong");

    // Helper methods
    private Guid GetTenantId() =>
        Guid.TryParse(Context.User?.FindFirstValue("tenant_id"), out var id) ? id : Guid.Empty;

    private Guid GetUserId() =>
        Guid.TryParse(Context.User?.FindFirstValue("sub")
            ?? Context.User?.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : Guid.Empty;

    private string GetRole() =>
        Context.User?.FindFirstValue(ClaimTypes.Role)
        ?? Context.User?.FindFirstValue("role") ?? "Unknown";

    // Static group name helpers — used by ISignalRService implementations
    public static string TenantGroup(Guid tenantId) => $"tenant:{tenantId}";
    public static string FleetGroup(Guid tenantId) => $"fleet:{tenantId}";
    public static string IncidentsGroup(Guid tenantId) => $"incidents:{tenantId}";
    public static string DispatchGroup(Guid tenantId) => $"dispatch:{tenantId}";
    public static string UserGroup(Guid userId) => $"user:{userId}";
    public static string RoleGroup(Guid tenantId, string role) => $"tenant:{tenantId}:role:{role}";
}

/// <summary>
/// Dedicated high-frequency hub for GPS fleet streaming.
/// Separated from OperationsHub to allow different scaling/backplane config.
/// </summary>
[Authorize]
public class FleetHub : Hub
{
    private readonly ILiveFleetCache _fleetCache;
    private readonly ILogger<FleetHub> _logger;

    public FleetHub(ILiveFleetCache fleetCache, ILogger<FleetHub> logger)
    {
        _fleetCache = fleetCache;
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        var tenantId = GetTenantId();
        if (tenantId == Guid.Empty) { Context.Abort(); return; }

        await Groups.AddToGroupAsync(Context.ConnectionId, $"fleet:{tenantId}");

        // Send current snapshot of all vehicle positions on connect
        var positions = await _fleetCache.GetAllPositionsAsync(tenantId);
        await Clients.Caller.SendAsync("FleetSnapshot", positions);

        await base.OnConnectedAsync();
    }

    [HubMethodName("RequestSnapshot")]
    public async Task RequestSnapshot()
    {
        var positions = await _fleetCache.GetAllPositionsAsync(GetTenantId());
        await Clients.Caller.SendAsync("FleetSnapshot", positions);
    }

    private Guid GetTenantId() =>
        Guid.TryParse(Context.User?.FindFirstValue("tenant_id"), out var id) ? id : Guid.Empty;
}

/// <summary>
/// Dispatch-specific hub for dispatchers and supervisors.
/// Restricted to Dispatcher role and above.
/// </summary>
[Authorize(Policy = "Dispatcher")]
public class DispatchHub : Hub
{
    private readonly ILogger<DispatchHub> _logger;

    public DispatchHub(ILogger<DispatchHub> logger) => _logger = logger;

    public override async Task OnConnectedAsync()
    {
        var tenantId = GetTenantId();
        if (tenantId == Guid.Empty) { Context.Abort(); return; }

        await Groups.AddToGroupAsync(Context.ConnectionId, $"dispatch:{tenantId}");
        await base.OnConnectedAsync();
    }

    // Dispatcher sends acknowledgment of AI recommendation
    [HubMethodName("AcknowledgeRecommendation")]
    public async Task AcknowledgeRecommendation(string recommendationId, bool accepted)
    {
        var tenantId = GetTenantId();
        var userId = GetUserId();

        _logger.LogInformation("Dispatcher {UserId} {Action} recommendation {Id}",
            userId, accepted ? "accepted" : "rejected", recommendationId);

        // Broadcast to other dispatchers for situational awareness
        await Clients.OthersInGroup($"dispatch:{tenantId}").SendAsync(
            "RecommendationAcknowledged",
            new { recommendationId, accepted, dispatcherId = userId, timestamp = DateTime.UtcNow });
    }

    private Guid GetTenantId() =>
        Guid.TryParse(Context.User?.FindFirstValue("tenant_id"), out var id) ? id : Guid.Empty;

    private Guid GetUserId() =>
        Guid.TryParse(Context.User?.FindFirstValue("sub"), out var id) ? id : Guid.Empty;
}
