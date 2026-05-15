using System;
using System.Collections.Generic;

namespace CivicOps.Models
{
    public class ResidentUser
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty; // Demo only - plain text
        public string FullName { get; set; } = string.Empty;
        public string? PhoneNumber { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime LastLoginAt { get; set; } = DateTime.UtcNow;
        
        // Area preferences for alerts
        public List<string> FollowedSuburbs { get; set; } = new();
        public List<string> FollowedWards { get; set; } = new();
        
        // Submitted reports
        public List<string> SubmittedReportReferences { get; set; } = new();
        
        // Notification preferences
        public bool EmailNotifications { get; set; } = true;
        public bool SMSNotifications { get; set; } = false;
        public bool AlertNotifications { get; set; } = true;
    }
}
