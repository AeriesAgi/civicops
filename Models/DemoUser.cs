using System;
using System.Collections.Generic;

namespace CivicOps.Models
{
    public enum UserRole
    {
        Admin,
        Dispatcher,
        DepartmentResponder,
        Viewer
    }

    public class DemoUser
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty; // Demo only - plain text for simplicity
        public string DisplayName { get; set; } = string.Empty;
        public UserRole Role { get; set; }
        public Department? AssignedDepartment { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastLoginAt { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class DemoAuthSession
    {
        public string SessionId { get; set; } = Guid.NewGuid().ToString();
        public string UserId { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public UserRole Role { get; set; }
        public Department? AssignedDepartment { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddHours(8);
    }
}
