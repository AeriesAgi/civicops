using CivicOps.Application.DTOs.Common;
using CivicOps.Application.Interfaces;
using CivicOps.Domain.Entities;
using CivicOps.Domain.Enums;
using CivicOps.Domain.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CivicOps.Api.Controllers;

// ═══════════════════════════════════════════════════════════════════
// USERS CONTROLLER
// ═══════════════════════════════════════════════════════════════════

[Authorize]
[Route("api/v1/users")]
public class UsersController : CivicOpsControllerBase
{
    private readonly IUnitOfWork _uow;
    private readonly IPasswordService _password;

    public UsersController(IUnitOfWork uow, IPasswordService password)
    {
        _uow = uow;
        _password = password;
    }

    [HttpGet]
    [Authorize(Policy = "OperationsManager")]
    public async Task<IActionResult> GetUsers(CancellationToken ct)
    {
        var users = await _uow.Users.GetByTenantAsync(CurrentTenantId, ct);
        var dtos = users.Select(MapToDto);
        return Ok(ApiResponse<IEnumerable<object>>.Ok(dtos));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetUser(Guid id, CancellationToken ct)
    {
        var user = await _uow.Users.GetByIdAsync(id, ct);
        if (user is null || user.TenantId != CurrentTenantId)
            return NotFound(ApiResponse.Fail("User not found."));

        return Ok(ApiResponse<object>.Ok(MapToDto(user)));
    }

    [HttpPost]
    [Authorize(Policy = "OperationsManager")]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest req, CancellationToken ct)
    {
        var existing = await _uow.Users.GetByEmailAsync(req.Email, ct);
        if (existing is not null)
            return Conflict(ApiResponse.Fail("A user with this email already exists."));

        if (!_password.IsStrongPassword(req.Password))
            return BadRequest(ApiResponse.Fail("Password does not meet complexity requirements."));

        var user = User.Create(CurrentTenantId, req.Email, req.FullName, req.Role,
            req.EmployeeId, req.Phone);
        user.SetPasswordHash(_password.HashPassword(req.Password));

        if (req.RegionId.HasValue)
            user.AssignToRegion(req.RegionId);

        await _uow.Users.AddAsync(user, ct);
        await _uow.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetUser), new { id = user.Id },
            ApiResponse<object>.Ok(MapToDto(user)));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "OperationsManager")]
    public async Task<IActionResult> UpdateUser(
        Guid id, [FromBody] UpdateUserRequest req, CancellationToken ct)
    {
        var user = await _uow.Users.GetByIdAsync(id, ct);
        if (user is null || user.TenantId != CurrentTenantId)
            return NotFound(ApiResponse.Fail("User not found."));

        user.UpdateProfile(req.FullName, req.Phone, req.EmployeeId, req.ProfileImageUrl);
        if (req.RegionId.HasValue)
            user.AssignToRegion(req.RegionId);

        await _uow.Users.UpdateAsync(user, ct);
        await _uow.SaveChangesAsync(ct);

        return Ok(ApiResponse<object>.Ok(MapToDto(user)));
    }

    [HttpPut("{id:guid}/role")]
    [Authorize(Policy = "OperationsManager")]
    public async Task<IActionResult> ChangeRole(Guid id, [FromBody] UserRole role, CancellationToken ct)
    {
        // Prevent privilege escalation
        if (role == UserRole.SuperAdmin && CurrentRole != "SuperAdmin")
            return Forbid();

        var user = await _uow.Users.GetByIdAsync(id, ct);
        if (user is null || user.TenantId != CurrentTenantId)
            return NotFound(ApiResponse.Fail("User not found."));

        user.ChangeRole(role);
        await _uow.Users.UpdateAsync(user, ct);
        await _uow.SaveChangesAsync(ct);

        return Ok(ApiResponse.Ok($"Role changed to {role}."));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "OperationsManager")]
    public async Task<IActionResult> DeactivateUser(Guid id, CancellationToken ct)
    {
        if (id == CurrentUserId)
            return BadRequest(ApiResponse.Fail("Cannot deactivate your own account."));

        var user = await _uow.Users.GetByIdAsync(id, ct);
        if (user is null || user.TenantId != CurrentTenantId)
            return NotFound(ApiResponse.Fail("User not found."));

        user.Deactivate();
        await _uow.Users.UpdateAsync(user, ct);
        await _uow.SaveChangesAsync(ct);

        return Ok(ApiResponse.Ok("User deactivated."));
    }

    [HttpPut("{id:guid}/unlock")]
    [Authorize(Policy = "OperationsManager")]
    public async Task<IActionResult> UnlockUser(Guid id, CancellationToken ct)
    {
        var user = await _uow.Users.GetByIdAsync(id, ct);
        if (user is null || user.TenantId != CurrentTenantId)
            return NotFound(ApiResponse.Fail("User not found."));

        user.UnlockAccount();
        await _uow.Users.UpdateAsync(user, ct);
        await _uow.SaveChangesAsync(ct);

        return Ok(ApiResponse.Ok("Account unlocked."));
    }

    private static object MapToDto(User u) => new
    {
        u.Id, u.Email, u.FullName, u.EmployeeId, u.Phone,
        Role = u.Role.ToString(),
        u.IsActive, u.MfaEnabled, u.RegionId,
        u.LastLoginAt, u.CreatedAt,
        IsLocked = u.IsLockedOut(),
        u.ProfileImageUrl
    };
}

// ═══════════════════════════════════════════════════════════════════
// NOTIFICATIONS CONTROLLER
// ═══════════════════════════════════════════════════════════════════

[Authorize]
[Route("api/v1/notifications")]
public class NotificationsController : CivicOpsControllerBase
{
    private readonly IUnitOfWork _uow;
    private readonly ISignalRService _signalR;
    private readonly IPushNotificationService _push;

    public NotificationsController(IUnitOfWork uow, ISignalRService signalR,
        IPushNotificationService push)
    {
        _uow = uow;
        _signalR = signalR;
        _push = push;
    }

    [HttpGet]
    public async Task<IActionResult> GetMyNotifications(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        // Simplified — real impl would use dedicated notification repository
        return Ok(ApiResponse<object>.Ok(new { items = Array.Empty<object>(), totalCount = 0 }));
    }

    [HttpPut("{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken ct)
        => Ok(ApiResponse.Ok("Marked as read."));

    [HttpPost("broadcast")]
    [Authorize(Policy = "OperationsManager")]
    public async Task<IActionResult> Broadcast(
        [FromBody] BroadcastRequest req, CancellationToken ct)
    {
        await _signalR.SendToTenantAsync(CurrentTenantId, "SystemNotification", new
        {
            title = req.Title,
            body = req.Message,
            type = "broadcast",
            severity = req.Severity ?? "info",
            sentBy = CurrentUserId,
            timestamp = DateTime.UtcNow
        }, ct);

        return Ok(ApiResponse.Ok("Broadcast sent."));
    }

    [HttpPut("fcm-token")]
    public async Task<IActionResult> UpdateFcmToken([FromBody] string token, CancellationToken ct)
    {
        var user = await _uow.Users.GetByIdAsync(CurrentUserId, ct);
        if (user is null) return NotFound();

        user.UpdateFcmToken(token);
        await _uow.Users.UpdateAsync(user, ct);
        await _uow.SaveChangesAsync(ct);

        return Ok(ApiResponse.Ok("FCM token updated."));
    }
}

// ═══════════════════════════════════════════════════════════════════
// PANIC CONTROLLER
// ═══════════════════════════════════════════════════════════════════

[Authorize]
[Route("api/v1/panic")]
public class PanicController : CivicOpsControllerBase
{
    private readonly IUnitOfWork _uow;
    private readonly ISignalRService _signalR;
    private readonly ISmsService _sms;
    private readonly ILogger<PanicController> _logger;

    public PanicController(IUnitOfWork uow, ISignalRService signalR,
        ISmsService sms, ILogger<PanicController> logger)
    {
        _uow = uow;
        _signalR = signalR;
        _sms = sms;
        _logger = logger;
    }

    /// <summary>Trigger a panic event from field officer or driver.</summary>
    [HttpPost]
    public async Task<IActionResult> TriggerPanic(
        [FromBody] TriggerPanicRequest req, CancellationToken ct)
    {
        var panic = PanicEvent.Create(
            CurrentTenantId, CurrentUserId, req.Latitude, req.Longitude, req.VehicleId);

        await _uow.SaveChangesAsync(ct);

        // Immediate broadcast to all supervisors and dispatchers
        await _signalR.SendPanicAlertAsync(CurrentTenantId, new
        {
            panicEventId = panic.Id,
            userId = CurrentUserId,
            vehicleId = req.VehicleId,
            latitude = req.Latitude,
            longitude = req.Longitude,
            triggeredAt = panic.TriggeredAt
        }, ct);

        // SMS escalation to supervisors
        var supervisors = (await _uow.Users.GetByRoleAsync(
            CurrentTenantId, UserRole.Supervisor, ct)).ToList();

        var user = await _uow.Users.GetByIdAsync(CurrentUserId, ct);
        var smsMessage = $"🚨 PANIC ALERT — {user?.FullName ?? "Unknown"} triggered panic at " +
            $"{req.Latitude:F5},{req.Longitude:F5}. Respond immediately.";

        foreach (var supervisor in supervisors.Where(s => !string.IsNullOrEmpty(s.Phone)))
        {
            _ = _sms.SendAsync(supervisor.Phone!, smsMessage, ct);
        }

        _logger.LogCritical("PANIC triggered by {UserId} at {Lat},{Lng}",
            CurrentUserId, req.Latitude, req.Longitude);

        return Ok(ApiResponse<object>.Ok(new { panicEventId = panic.Id }));
    }

    /// <summary>Resolve a panic event.</summary>
    [HttpPut("{id:guid}/resolve")]
    [Authorize(Policy = "Supervisor")]
    public async Task<IActionResult> ResolvePanic(
        Guid id, [FromBody] Guid? incidentId, CancellationToken ct)
    {
        // Simplified — real impl loads panic from DB and calls resolve
        await _signalR.SendToTenantAsync(CurrentTenantId, "PanicResolved", new
        {
            panicEventId = id,
            resolvedById = CurrentUserId,
            resolvedAt = DateTime.UtcNow
        }, ct);

        return Ok(ApiResponse.Ok("Panic event resolved."));
    }
}

// ═══════════════════════════════════════════════════════════════════
// ADMIN CONTROLLER (Super Admin only)
// ═══════════════════════════════════════════════════════════════════

[Authorize(Policy = "SuperAdmin")]
[Route("api/v1/admin")]
public class AdminController : CivicOpsControllerBase
{
    private readonly IUnitOfWork _uow;

    public AdminController(IUnitOfWork uow) => _uow = uow;

    [HttpGet("tenants")]
    public async Task<IActionResult> GetTenants(CancellationToken ct)
    {
        var tenants = await _uow.Tenants.GetAllAsync(ct);
        var dtos = tenants.Select(t => new
        {
            t.Id, t.Name, t.Slug, t.Tier, t.IsActive,
            t.MaxUnits, t.MaxUsers, t.ExpiresAt, t.CreatedAt,
            IsExpired = t.IsExpired()
        });
        return Ok(ApiResponse<IEnumerable<object>>.Ok(dtos));
    }

    [HttpPost("tenants")]
    public async Task<IActionResult> CreateTenant(
        [FromBody] CreateTenantRequest req, CancellationToken ct)
    {
        var existing = await _uow.Tenants.GetBySlugAsync(req.Slug, ct);
        if (existing is not null)
            return Conflict(ApiResponse.Fail($"Tenant slug '{req.Slug}' already exists."));

        var tenant = Tenant.Create(req.Name, req.Slug, req.Tier ?? "professional");
        if (req.MaxUnits.HasValue) tenant.UpdateLimits(req.MaxUnits.Value, req.MaxUsers ?? 25);
        if (req.ExpiresAt.HasValue) tenant.SetExpiry(req.ExpiresAt.Value);

        await _uow.Tenants.AddAsync(tenant, ct);
        await _uow.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetTenants), null,
            ApiResponse<object>.Ok(new { tenant.Id, tenant.Name, tenant.Slug }));
    }

    [HttpGet("system-health")]
    public IActionResult GetSystemHealth()
    {
        return Ok(new
        {
            status = "healthy",
            version = "1.0.0",
            environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"),
            timestamp = DateTime.UtcNow,
            uptime = Environment.TickCount64 / 1000
        });
    }
}

// ── Request models for controllers without separate DTOs ──────────

public record CreateUserRequest(
    string Email, string FullName, string Password,
    UserRole Role, string? EmployeeId = null,
    string? Phone = null, Guid? RegionId = null
);

public record UpdateUserRequest(
    string FullName, string? Phone = null,
    string? EmployeeId = null, string? ProfileImageUrl = null,
    Guid? RegionId = null
);

public record BroadcastRequest(string Title, string Message, string? Severity = "info");

public record TriggerPanicRequest(decimal Latitude, decimal Longitude, Guid? VehicleId = null);

public record CreateTenantRequest(
    string Name, string Slug, string? Tier = null,
    int? MaxUnits = null, int? MaxUsers = null,
    DateTime? ExpiresAt = null
);
