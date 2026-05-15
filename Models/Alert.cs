using System;

namespace CivicOps.Models
{
    public enum AlertType
    {
        WaterOutage,
        ElectricityDisruption,
        RoadClosure,
        Flood,
        Fire,
        WasteCollectionDisruption,
        EnvironmentalHazard,
        PublicSafetyNotice,
        DisasterWarning
    }

    public enum AlertSeverity
    {
        Info,
        Warning,
        Urgent,
        Critical
    }

    public class Alert
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public AlertType Type { get; set; }
        public AlertSeverity Severity { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Suburb { get; set; } = string.Empty;
        public string Ward { get; set; } = string.Empty;
        public Department AffectedDepartment { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ExpiresAt { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
