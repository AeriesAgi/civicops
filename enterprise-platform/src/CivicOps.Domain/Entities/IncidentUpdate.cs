namespace CivicOps.Domain.Entities;

public class IncidentUpdate : TenantEntity
{
    public Guid IncidentId { get; private set; }
    public Guid AuthorId { get; private set; }
    public string Type { get; private set; } = "note";
    public string? Note { get; private set; }
    public List<string> MediaUrls { get; private set; } = new();
    public bool IsInternal { get; private set; } = false;

    public Incident? Incident { get; private set; }
    public User? Author { get; private set; }

    private IncidentUpdate() { }

    public static IncidentUpdate Create(Guid tenantId, Guid incidentId, Guid authorId,
        string type, string? note, bool isInternal = false, List<string>? mediaUrls = null)
    {
        return new IncidentUpdate
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            IncidentId = incidentId,
            AuthorId = authorId,
            Type = type,
            Note = note,
            IsInternal = isInternal,
            MediaUrls = mediaUrls ?? new List<string>(),
            CreatedAt = DateTime.UtcNow
        };
    }
}

public class IncidentMedia : TenantEntity
{
    public Guid IncidentId { get; private set; }
    public Guid UploaderId { get; private set; }
    public string Type { get; private set; } = "image";
    public string FileName { get; private set; } = string.Empty;
    public string StorageKey { get; private set; } = string.Empty;
    public long? SizeBytes { get; private set; }
    public string? MimeType { get; private set; }
    public int? DurationSeconds { get; private set; }
    public string? ThumbnailKey { get; private set; }

    public Incident? Incident { get; private set; }
    public User? Uploader { get; private set; }

    private IncidentMedia() { }

    public static IncidentMedia Create(Guid tenantId, Guid incidentId, Guid uploaderId,
        string type, string fileName, string storageKey,
        long? sizeBytes = null, string? mimeType = null,
        int? durationSeconds = null, string? thumbnailKey = null)
    {
        return new IncidentMedia
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            IncidentId = incidentId,
            UploaderId = uploaderId,
            Type = type,
            FileName = fileName,
            StorageKey = storageKey,
            SizeBytes = sizeBytes,
            MimeType = mimeType,
            DurationSeconds = durationSeconds,
            ThumbnailKey = thumbnailKey,
            CreatedAt = DateTime.UtcNow
        };
    }
}
