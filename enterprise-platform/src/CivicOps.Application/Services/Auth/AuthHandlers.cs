using CivicOps.Application.Commands.Auth;
using CivicOps.Application.DTOs.Auth;
using CivicOps.Application.DTOs.Common;
using CivicOps.Application.Interfaces;
using CivicOps.Domain.Entities;
using CivicOps.Domain.Enums;
using CivicOps.Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CivicOps.Application.Services.Auth;

public class LoginCommandHandler : IRequestHandler<LoginCommand, Result<LoginResponse>>
{
    private readonly IUserRepository _users;
    private readonly ITokenService _tokenService;
    private readonly IPasswordService _passwordService;
    private readonly ITenantRepository _tenants;
    private readonly ILogger<LoginCommandHandler> _logger;

    public LoginCommandHandler(IUserRepository users, ITokenService tokenService,
        IPasswordService passwordService, ITenantRepository tenants,
        ILogger<LoginCommandHandler> logger)
    {
        _users = users;
        _tokenService = tokenService;
        _passwordService = passwordService;
        _tenants = tenants;
        _logger = logger;
    }

    public async Task<Result<LoginResponse>> Handle(LoginCommand request, CancellationToken ct)
    {
        var user = await _users.GetByEmailAsync(request.Email.ToLowerInvariant(), ct);
        if (user is null)
            return Result.Failure<LoginResponse>("Invalid credentials.");

        if (!user.IsActive)
            return Result.Failure<LoginResponse>("Account is deactivated.");

        if (user.IsLockedOut())
            return Result.Failure<LoginResponse>("Account locked. Please try again later.");

        if (!_passwordService.VerifyPassword(request.Password, user.PasswordHash))
        {
            user.RecordFailedLogin();
            await _users.UpdateAsync(user, ct);
            return Result.Failure<LoginResponse>("Invalid credentials.");
        }

        if (user.MfaEnabled)
        {
            // Return partial login — client must verify MFA next
            return Result.Success(new LoginResponse(
                AccessToken: string.Empty,
                RefreshToken: string.Empty,
                User: MapToProfile(user),
                RequiresMfa: true
            ));
        }

        var tenant = await _tenants.GetByIdAsync(user.TenantId, ct);
        if (tenant is null || !tenant.IsActive || tenant.IsExpired())
            return Result.Failure<LoginResponse>("Organization account is not active.");

        var accessToken = _tokenService.GenerateAccessToken(user, tenant);
        var refreshTokenStr = _tokenService.GenerateRefreshToken();

        var refreshToken = RefreshToken.Create(user.Id, refreshTokenStr,
            deviceId: request.DeviceId,
            ipAddress: request.IpAddress,
            userAgent: null);

        await _users.AddRefreshTokenAsync(refreshToken, ct);

        user.RecordLogin();
        if (!string.IsNullOrEmpty(request.FcmToken))
            user.UpdateFcmToken(request.FcmToken);

        await _users.UpdateAsync(user, ct);

        _logger.LogInformation("User {Email} logged in from {IP}", user.Email, request.IpAddress);

        return Result.Success(new LoginResponse(
            AccessToken: accessToken,
            RefreshToken: refreshTokenStr,
            User: MapToProfile(user),
            RequiresMfa: false
        ));
    }

    private static UserProfileDto MapToProfile(User user) => new(
        user.Id, user.TenantId, user.Email, user.FullName, user.EmployeeId,
        user.Role.ToString(), user.IsActive, user.MfaEnabled, user.Phone,
        user.ProfileImageUrl, user.RegionId, user.LastLoginAt, user.CreatedAt
    );
}

public class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, Result<RefreshTokenResponse>>
{
    private readonly IUserRepository _users;
    private readonly ITokenService _tokenService;
    private readonly ITenantRepository _tenants;

    public RefreshTokenCommandHandler(IUserRepository users, ITokenService tokenService, ITenantRepository tenants)
    {
        _users = users;
        _tokenService = tokenService;
        _tenants = tenants;
    }

    public async Task<Result<RefreshTokenResponse>> Handle(RefreshTokenCommand request, CancellationToken ct)
    {
        var existingToken = await _users.GetRefreshTokenAsync(request.RefreshToken, ct);
        if (existingToken is null || !existingToken.IsActive)
            return Result.Failure<RefreshTokenResponse>("Invalid or expired refresh token.");

        var user = await _users.GetByIdAsync(existingToken.UserId, ct);
        if (user is null || !user.IsActive)
            return Result.Failure<RefreshTokenResponse>("User account not found or inactive.");

        var tenant = await _tenants.GetByIdAsync(user.TenantId, ct);
        if (tenant is null || !tenant.IsActive)
            return Result.Failure<RefreshTokenResponse>("Organization account not active.");

        var newRefreshTokenStr = _tokenService.GenerateRefreshToken();
        var newRefreshToken = RefreshToken.Create(user.Id, newRefreshTokenStr,
            deviceId: request.DeviceId, ipAddress: request.IpAddress);

        existingToken.Revoke(newRefreshTokenStr);
        await _users.UpdateAsync(user, ct);
        await _users.AddRefreshTokenAsync(newRefreshToken, ct);

        var accessToken = _tokenService.GenerateAccessToken(user, tenant);

        return Result.Success(new RefreshTokenResponse(accessToken, newRefreshTokenStr));
    }
}

public class LogoutCommandHandler : IRequestHandler<LogoutCommand, Result>
{
    private readonly IUserRepository _users;

    public LogoutCommandHandler(IUserRepository users) => _users = users;

    public async Task<Result> Handle(LogoutCommand request, CancellationToken ct)
    {
        var token = await _users.GetRefreshTokenAsync(request.RefreshToken, ct);
        if (token is not null && token.IsActive)
        {
            token.Revoke();
            await _users.UpdateAsync(await _users.GetByIdAsync(token.UserId, ct) ?? throw new Exception(), ct);
        }
        return Result.Success();
    }
}

public class SetupMfaCommandHandler : IRequestHandler<SetupMfaCommand, Result<MfaSetupResponse>>
{
    private readonly IUserRepository _users;
    private readonly IMfaService _mfa;

    public SetupMfaCommandHandler(IUserRepository users, IMfaService mfa)
    {
        _users = users;
        _mfa = mfa;
    }

    public async Task<Result<MfaSetupResponse>> Handle(SetupMfaCommand request, CancellationToken ct)
    {
        var user = await _users.GetByIdAsync(request.UserId, ct);
        if (user is null) return Result.Failure<MfaSetupResponse>("User not found.");

        var secret = _mfa.GenerateSecret();
        var qrUri = _mfa.GenerateQrCodeUri(secret, user.Email, "CivicOps Command");
        var backupCodes = Enumerable.Range(0, 8).Select(_ => _mfa.GenerateBackupCode()).ToList();

        user.EnableMfa(secret);
        await _users.UpdateAsync(user, ct);

        return Result.Success(new MfaSetupResponse(secret, qrUri, backupCodes));
    }
}
