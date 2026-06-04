namespace CivicOps.Domain.Entities;

public class Tenant : BaseEntity
{
    public string Name { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;
    public string Tier { get; private set; } = "professional";
    public bool IsActive { get; private set; } = true;
    public int MaxUnits { get; private set; } = 50;
    public int MaxUsers { get; private set; } = 25;
    public DateTime? ExpiresAt { get; private set; }

    // Branding (stored as JSON)
    public string? LogoUrl { get; private set; }
    public string? PrimaryColor { get; private set; }
    public string? SecondaryColor { get; private set; }
    public string? CustomDomain { get; private set; }
    public string? CompanyName { get; private set; }
    public string? SupportEmail { get; private set; }

    // Settings (stored as JSON)
    public bool AiDispatchEnabled { get; private set; } = true;
    public bool PredictiveMaintenanceEnabled { get; private set; } = true;
    public bool WhatsAppIntegrationEnabled { get; private set; } = false;
    public bool ClientViewerPortalEnabled { get; private set; } = false;
    public int DefaultSlaMinutes { get; private set; } = 10;
    public string TimeZone { get; private set; } = "Africa/Johannesburg";

    // Navigation
    public ICollection<User> Users { get; private set; } = new List<User>();
    public ICollection<Vehicle> Vehicles { get; private set; } = new List<Vehicle>();
    public ICollection<Incident> Incidents { get; private set; } = new List<Incident>();
    public ICollection<Region> Regions { get; private set; } = new List<Region>();

    private Tenant() { }

    public static Tenant Create(string name, string slug, string tier = "professional")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);

        return new Tenant
        {
            Id = Guid.NewGuid(),
            Name = name,
            Slug = slug.ToLowerInvariant(),
            Tier = tier,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void UpdateBranding(string? logoUrl, string? primaryColor, string? secondaryColor,
        string? customDomain, string? companyName, string? supportEmail)
    {
        LogoUrl = logoUrl;
        PrimaryColor = primaryColor;
        SecondaryColor = secondaryColor;
        CustomDomain = customDomain;
        CompanyName = companyName;
        SupportEmail = supportEmail;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateLimits(int maxUnits, int maxUsers)
    {
        MaxUnits = maxUnits;
        MaxUsers = maxUsers;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Deactivate() { IsActive = false; UpdatedAt = DateTime.UtcNow; }
    public void Activate() { IsActive = true; UpdatedAt = DateTime.UtcNow; }

    public void SetExpiry(DateTime expiresAt)
    {
        ExpiresAt = expiresAt;
        UpdatedAt = DateTime.UtcNow;
    }

    public bool IsExpired() => ExpiresAt.HasValue && ExpiresAt.Value < DateTime.UtcNow;
}
