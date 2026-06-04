using CivicOps.Domain.Enums;
using MediatR;

namespace CivicOps.Application.DTOs.Auth;

public record LoginRequest(string Email, string Password, string? DeviceId, string? FcmToken);
public record LoginResponse(string AccessToken, string RefreshToken, UserProfileDto User, bool RequiresMfa);
public record RefreshTokenRequest(string RefreshToken, string? DeviceId);
public record RefreshTokenResponse(string AccessToken, string RefreshToken);
public record MfaSetupResponse(string Secret, string QrCodeUri, IEnumerable<string> BackupCodes);
public record MfaVerifyRequest(string Code);
public record ChangePasswordRequest(string CurrentPassword, string NewPassword);
public record ForgotPasswordRequest(string Email);
public record ResetPasswordRequest(string Token, string NewPassword);

public record UserProfileDto(
    Guid Id,
    Guid TenantId,
    string Email,
    string FullName,
    string? EmployeeId,
    string Role,
    bool IsActive,
    bool MfaEnabled,
    string? Phone,
    string? ProfileImageUrl,
    Guid? RegionId,
    DateTime? LastLoginAt,
    DateTime CreatedAt
);

namespace CivicOps.Application.Commands.Auth;

using CivicOps.Application.DTOs.Auth;
using CivicOps.Application.DTOs.Common;

public record LoginCommand(
    string Email,
    string Password,
    string? DeviceId = null,
    string? IpAddress = null,
    string? UserAgent = null,
    string? FcmToken = null
) : IRequest<Result<LoginResponse>>;

public record RefreshTokenCommand(
    string RefreshToken,
    string? DeviceId = null,
    string? IpAddress = null
) : IRequest<Result<RefreshTokenResponse>>;

public record LogoutCommand(string RefreshToken) : IRequest<Result>;

public record SetupMfaCommand(Guid UserId) : IRequest<Result<MfaSetupResponse>>;

public record VerifyMfaCommand(Guid UserId, string Code) : IRequest<Result>;

public record ChangePasswordCommand(
    Guid UserId,
    string CurrentPassword,
    string NewPassword
) : IRequest<Result>;
