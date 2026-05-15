using System;

namespace CivicOps.Models
{
    public class IncidentStatusHistory
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public IncidentStatus FromStatus { get; set; }
        public IncidentStatus ToStatus { get; set; }
        public string ChangedBy { get; set; } = "System";
        public string? Reason { get; set; }
    }
}
