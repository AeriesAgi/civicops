using System;
using System.Collections.Generic;
using System.Linq;

namespace CivicOps.Models
{
    public enum IncidentStatus
    {
        New,
        Triaged,
        Assigned,
        Acknowledged,
        InProgress,
        WaitingForCitizen,
        Escalated,
        AlertRecommended,
        Resolved,
        Closed
    }

    public enum IncidentPriority
    {
        Low,
        Medium,
        High,
        Urgent,
        Critical
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
        public string RawDescription { get; set; } = string.Empty;
        public string CleanedDescription { get; set; } = string.Empty;
        public string OriginalArea { get; set; } = string.Empty;
        public string NormalizedArea { get; set; } = string.Empty;
        public string Municipality { get; set; } = "eThekwini";
        public string WardConfidence { get; set; } = "Needs ward confirmation";
        public string EnrichmentSource { get; set; } = "Local deterministic fallback";
        public string EnrichmentNotes { get; set; } = string.Empty;
        public string RoutingConfidence { get; set; } = "Medium";
        public bool EmergencyReferralRecommended { get; set; }
        public string CitizenResponse { get; set; } = string.Empty;
        public string DepartmentBrief { get; set; } = string.Empty;
        public string AlertRecommendation { get; set; } = string.Empty;
        public string RoutingReason { get; set; } = string.Empty;
        public string AISummary { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public Department AssignedDepartment { get; set; }
        public string Suburb { get; set; } = string.Empty;
        public string Ward { get; set; } = string.Empty;
        public IncidentStatus Status { get; set; } = IncidentStatus.New;
        public IncidentPriority Priority { get; set; } = IncidentPriority.Medium;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime LastUpdatedAt { get; set; } = DateTime.UtcNow;
        public List<PublicUpdate> PublicUpdates { get; set; } = new();
        public List<IncidentNote> InternalNotes { get; set; } = new();
        public List<IncidentStatusHistory> StatusHistory { get; set; } = new();
        public List<MediaAttachment> MediaAttachments { get; set; } = new();
        public string? ContactName { get; set; }
        public string? ContactPhone { get; set; }
        public string MaskedContactPhone => MaskPhone(ContactPhone);
        public string? ContactEmail { get; set; }
        public string? LocationNotes { get; set; }
        public string? MediaMetadata { get; set; }
        public string? AudioMetadata { get; set; }
        public Dictionary<string, string> ConnectorMetadata { get; set; } = new();
        public bool IsGeminiProcessed { get; set; }
        public string ClassificationMethod { get; set; } = "Deterministic";
        public int AffectedCount { get; set; }
        public string CommunityThreadSummary { get; set; } = string.Empty;

        private static string MaskPhone(string? phoneNumber)
        {
            if (string.IsNullOrWhiteSpace(phoneNumber))
            {
                return string.Empty;
            }

            var digits = new string(phoneNumber.Where(char.IsDigit).ToArray());
            if (digits.Length <= 4)
            {
                return "****";
            }

            return $"***{digits[^4..]}";
        }
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
