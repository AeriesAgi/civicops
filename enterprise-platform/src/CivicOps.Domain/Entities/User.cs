using CivicOps.Domain.Enums;

namespace CivicOps.Domain.Entities;

public class User : TenantEntity
{
    public string Email { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public string FullName { get; private set; } = string.Empty;
    public string? EmployeeId { get; private set; }
    public UserRole Role { get; private set; }
    public bool IsActive { get; private set; } = true;
    public bool MfaEnabled { get; private set; } = false;
    public string? MfaSecret { get; private set; }
    public string? Phone { get; private set; }
    public Guid? RegionId { get; private set; }
    public DateTime? LastLoginAt { get; private set; }
    public string? FcmToken { get; private set; }
    public string? ProfileImageUrl { get; private set; }
    public int FailedLoginAttempts { get; private set; }
    public DateTime? LockedUntil { get; private set; }

    // Navigation
    public Tenant? Tenant { get; private set; }
    public Region? Region { get; private set; }
    public ICollection<RefreshToken> RefreshTokens { get; private set; } = new List<RefreshToken>();
    public ICollection<DispatchAssignment> Dispatches { get; private set; } = new List<DispatchAssignment>();

    private User() { }

    public static User Create(Guid tenantId, string email, string fullName,
        UserRole role, string? employeeId = null, string? phone = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(fullName);

        return new User
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Email = email.ToLowerInvariant().Trim(),
            FullName = fullName.Trim(),
            Role = role,
            EmployeeId = employeeId,
            Phone = phone,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void SetPasswordHash(string hash)
    {
        PasswordHash = hash;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateProfile(string fullName, string? phone, string? employeeId, string? profileImageUrl)
    {
        FullName = fullName;
        Phone = phone;
        EmployeeId = employeeId;
        ProfileImageUrl = profileImageUrl;
        UpdatedAt = DateTime.UtcNow;
    }

    public void ChangeRole(UserRole role)
    {
        Role = role;
        UpdatedAt = DateTime.UtcNow;
    }

    public void AssignToRegion(Guid? regionId)
    {
        RegionId = regionId;
        UpdatedAt = DateTime.UtcNow;
    }

    public void RecordLogin()
    {
        LastLoginAt = DateTime.UtcNow;
        FailedLoginAttempts = 0;
        LockedUntil = null;
    }

    public void RecordFailedLogin()
    {
        FailedLoginAttempts++;
        if (FailedLoginAttempts >= 5)
        {
            LockedUntil = DateTime.UtcNow.AddMinutes(30);
        }
        UpdatedAt = DateTime.UtcNow;
    }

    public bool IsLockedOut() => LockedUntil.HasValue && LockedUntil.Value > DateTime.UtcNow;

    public void EnableMfa(string secret)
    {
        MfaEnabled = true;
        MfaSecret = secret;
        UpdatedAt = DateTime.UtcNow;
    }

    public void DisableMfa()
    {
        MfaEnabled = false;
        MfaSecret = null;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateFcmToken(string? token)
    {
        FcmToken = token;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Deactivate() { IsActive = false; UpdatedAt = DateTime.UtcNow; }
    public void Activate() { IsActive = true; UpdatedAt = DateTime.UtcNow; }

    public void UnlockAccount()
    {
        LockedUntil = null;
        FailedLoginAttempts = 0;
        UpdatedAt = DateTime.UtcNow;
    }
}
