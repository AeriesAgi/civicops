using System;
using System.Collections.Generic;

namespace CivicOps.Models
{
    public enum IncidentStatus
    {
        New,
        Triaged,
        Assigned,
        InProgress,
        Escalated,
        Resolved,
        Closed
    }

    public enum IncidentPriority
    {
        Low,
        Medium,
        High,
        Urgent
    }

    public enum SourceChannel
    {
        Web,
        Android,
        WhatsApp,
        VoiceNote,
        Demo
    }

    public class Incident
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string ReferenceNumber { get; set; } = string.Empty;
        public SourceChannel SourceChannel { get; set; }
        public string Description { get; set; } = string.Empty;
        public string AISummary { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public Department AssignedDepartment { get; set; }
        public string Suburb { get; set; } = string.Empty;
        public string Ward { get; set; } = string.Empty;
        public IncidentStatus Status { get; set; } = IncidentStatus.New;
        public IncidentPriority Priority { get; set; } = IncidentPriority.Medium;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime LastUpdatedAt { get; set; } = DateTime.UtcNow;
        public List<string> PublicUpdates { get; set; } = new();
        public List<IncidentNote> InternalNotes { get; set; } = new();
        public string? ContactName { get; set; }
        public string? ContactPhone { get; set; }
        public string? ContactEmail { get; set; }
        public string? LocationNotes { get; set; }
        public string? MediaMetadata { get; set; }
        public string? AudioMetadata { get; set; }
        public Dictionary<string, string> ConnectorMetadata { get; set; } = new();
        public bool IsGeminiProcessed { get; set; }
        public string ClassificationMethod { get; set; } = "Deterministic";
    }

    public class IncidentNote
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string Author { get; set; } = "System";
        public string Content { get; set; } = string.Empty;
        public bool IsPublic { get; set; }
    }
}
