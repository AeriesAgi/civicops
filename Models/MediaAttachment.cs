using System;

namespace CivicOps.Models
{
    public enum MediaType
    {
        Image,
        Audio,
        Video,
        Document
    }

    public class MediaAttachment
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string IncidentId { get; set; } = string.Empty;
        public MediaType Type { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string? FilePath { get; set; }
        public string? Url { get; set; }
        public long FileSize { get; set; }
        public string? MimeType { get; set; }
        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
        public string UploadedBy { get; set; } = "System";
        public string? Description { get; set; }
        public bool IsProcessed { get; set; }
        public string? TranscriptionText { get; set; }
    }
}
