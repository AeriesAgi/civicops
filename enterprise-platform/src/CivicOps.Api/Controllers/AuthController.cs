using CivicOps.Application.Commands.Auth;
using CivicOps.Application.DTOs.Auth;
using CivicOps.Application.DTOs.Common;
using CivicOps.Application.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CivicOps.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
public abstract class CivicOpsControllerBase : ControllerBase
{
    protected Guid CurrentUserId
    {
        get
        {
            var claim = User.FindFirst("sub")?.Value
                ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(claim, out var id) ? id : Guid.Empty;
        }
    }

    protected Guid CurrentTenantId
    {
        get
        {
            var claim = User.FindFirst("tenant_id")?.Value;
            if (Guid.TryParse(claim, out var id)) return id;

            if (HttpContext.Items.TryGetValue("TenantId", out var tid) && tid is Guid g) return g;
            return Guid.Empty;
        }
    }

    protected string CurrentRole =>
        User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value
        ?? User.FindFirst("role")?.Value ?? string.Empty;

    protected IActionResult OkResult<T>(T data, string? message = null)
        => Ok(ApiResponse<T>.Ok(data, message));

    protected IActionResult FailResult(string message, int statusCode = 400)
        => StatusCode(statusCode, ApiResponse.Fail(message));

    protected IActionResult FromResult<T>(Result<T> result)
        => result.IsSuccess ? Ok(ApiResponse<T>.Ok(result.Value!)) : BadRequest(ApiResponse.Fail(result.Error!));

    protected IActionResult FromResult(Result result)
        => result.IsSuccess ? Ok(ApiResponse.Ok()) : BadRequest(ApiResponse.Fail(result.Error!));
}

[Route("api/v1/auth")]
[EnableRateLimiting("auth")]
public class AuthController : CivicOpsControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICurrentUserContext _currentUser;

    public AuthController(IMediator mediator, ICurrentUserContext currentUser)
    {
        _mediator = mediator;
        _currentUser = currentUser;
    }

    /// <summary>Login with email and password. Returns JWT tokens.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest req, CancellationToken ct)
    {
        var command = new LoginCommand(
            req.Email, req.Password, req.DeviceId, req.FcmToken,
            IpAddress: HttpContext.Connection.RemoteIpAddress?.ToString()
        );

        var result = await _mediator.Send(command, ct);
        return FromResult(result);
    }

    /// <summary>Refresh access token using a valid refresh token.</summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequest req, CancellationToken ct)
    {
        var command = new RefreshTokenCommand(
            req.RefreshToken, req.DeviceId,
            HttpContext.Connection.RemoteIpAddress?.ToString()
        );

        var result = await _mediator.Send(command, ct);
        return FromResult(result);
    }

    /// <summary>Logout and revoke refresh token.</summary>
    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout([FromBody] string refreshToken, CancellationToken ct)
    {
        var result = await _mediator.Send(new LogoutCommand(refreshToken), ct);
        return FromResult(result);
    }

    /// <summary>Setup MFA — returns QR code URI and backup codes.</summary>
    [HttpPost("mfa/setup")]
    [Authorize]
    public async Task<IActionResult> SetupMfa(CancellationToken ct)
    {
        var result = await _mediator.Send(new SetupMfaCommand(CurrentUserId), ct);
        return FromResult(result);
    }

    /// <summary>Verify MFA code to complete setup or login.</summary>
    [HttpPost("mfa/verify")]
    [Authorize]
    public async Task<IActionResult> VerifyMfa([FromBody] MfaVerifyRequest req, CancellationToken ct)
    {
        var result = await _mediator.Send(new VerifyMfaCommand(CurrentUserId, req.Code), ct);
        return FromResult(result);
    }

    /// <summary>Change current user's password.</summary>
    [HttpPut("password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest req, CancellationToken ct)
    {
        var result = await _mediator.Send(
            new ChangePasswordCommand(CurrentUserId, req.CurrentPassword, req.NewPassword), ct);
        return FromResult(result);
    }

    /// <summary>Returns the current user's profile from JWT claims.</summary>
    [HttpGet("me")]
    [Authorize]
    public IActionResult Me()
    {
        return Ok(new
        {
            userId = CurrentUserId,
            tenantId = CurrentTenantId,
            email = User.FindFirst("email")?.Value,
            fullName = User.FindFirst("full_name")?.Value,
            role = CurrentRole
        });
    }
}
