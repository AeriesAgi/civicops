using System;

namespace CivicOps.Models
{
    public class PublicUpdate
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string Content { get; set; } = string.Empty;
        public string UpdatedBy { get; set; } = "System";
        public IncidentStatus? RelatedStatus { get; set; }
    }
}
