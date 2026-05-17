using CivicOps.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CivicOps.Services
{
    public class DemoAuthService : IDemoAuthService
    {
        private readonly List<DemoUser> _users = new();
        private readonly Dictionary<string, DemoAuthSession> _sessions = new();

        public Task InitializeAsync()
        {
            // Seed demo users
            _users.AddRange(new[]
            {
                new DemoUser
                {
                    Email = "admin@civicops.demo",
                    Password = "CivicOps2026!",
                    DisplayName = "Admin User",
                    Role = UserRole.Admin,
                    IsActive = true
                },
                new DemoUser
                {
                    Email = "dispatcher@civicops.demo",
                    Password = "CivicOps2026!",
                    DisplayName = "Dispatcher",
                    Role = UserRole.Dispatcher,
                    IsActive = true
                },
                new DemoUser
                {
                    Email = "water@civicops.demo",
                    Password = "CivicOps2026!",
                    DisplayName = "Water Department",
                    Role = UserRole.DepartmentResponder,
                    AssignedDepartment = Department.WaterAndSanitation,
                    IsActive = true
                },
                new DemoUser
                {
                    Email = "electricity@civicops.demo",
                    Password = "CivicOps2026!",
                    DisplayName = "Electricity Department",
                    Role = UserRole.DepartmentResponder,
                    AssignedDepartment = Department.Electricity,
                    IsActive = true
                },
                new DemoUser
                {
                    Email = "roads@civicops.demo",
                    Password = "CivicOps2026!",
                    DisplayName = "Roads & Stormwater Department",
                    Role = UserRole.DepartmentResponder,
                    AssignedDepartment = Department.RoadsAndStormwater,
                    IsActive = true
                },
                new DemoUser { Email = "waste@civicops.demo", Password = "CivicOps2026!", DisplayName = "Waste Management Department", Role = UserRole.DepartmentResponder, AssignedDepartment = Department.WasteManagement, IsActive = true },
                new DemoUser { Email = "parks@civicops.demo", Password = "CivicOps2026!", DisplayName = "Parks & Public Spaces Department", Role = UserRole.DepartmentResponder, AssignedDepartment = Department.ParksAndPublicSpaces, IsActive = true },
                new DemoUser { Email = "health@civicops.demo", Password = "CivicOps2026!", DisplayName = "Environmental Health Department", Role = UserRole.DepartmentResponder, AssignedDepartment = Department.EnvironmentalHealth, IsActive = true },
                new DemoUser { Email = "disaster@civicops.demo", Password = "CivicOps2026!", DisplayName = "Disaster Management Department", Role = UserRole.DepartmentResponder, AssignedDepartment = Department.DisasterManagement, IsActive = true },
                new DemoUser { Email = "fire@civicops.demo", Password = "CivicOps2026!", DisplayName = "Fire & Rescue Department", Role = UserRole.DepartmentResponder, AssignedDepartment = Department.FireAndRescue, IsActive = true },
                new DemoUser { Email = "safety@civicops.demo", Password = "CivicOps2026!", DisplayName = "Metro Police/Public Safety Department", Role = UserRole.DepartmentResponder, AssignedDepartment = Department.MetroPolicePublicSafety, IsActive = true },
                new DemoUser { Email = "resident@civicops.demo", Password = "CivicOps2026!", DisplayName = "Resident Demo", Role = UserRole.Viewer, IsActive = true }
            });

            return Task.CompletedTask;
        }

        public Task<DemoAuthSession?> LoginAsync(string email, string password)
        {
            var user = _users.FirstOrDefault(u => 
                u.Email.Equals(email, StringComparison.OrdinalIgnoreCase) && 
                u.Password == password && 
                u.IsActive);

            if (user == null)
            {
                return Task.FromResult<DemoAuthSession?>(null);
            }

            // Clean up expired sessions
            CleanupExpiredSessions();

            // Create new session
            var session = new DemoAuthSession
            {
                UserId = user.Id,
                Email = user.Email,
                Role = user.Role,
                AssignedDepartment = user.AssignedDepartment
            };

            _sessions[session.SessionId] = session;
            user.LastLoginAt = DateTime.UtcNow;

            return Task.FromResult<DemoAuthSession?>(session);
        }

        public Task LogoutAsync(string sessionId)
        {
            _sessions.Remove(sessionId);
            return Task.CompletedTask;
        }

        public Task<DemoAuthSession?> GetSessionAsync(string sessionId)
        {
            if (string.IsNullOrEmpty(sessionId))
            {
                return Task.FromResult<DemoAuthSession?>(null);
            }

            if (_sessions.TryGetValue(sessionId, out var session))
            {
                if (session.ExpiresAt > DateTime.UtcNow)
                {
                    return Task.FromResult<DemoAuthSession?>(session);
                }
                
                // Session expired
                _sessions.Remove(sessionId);
            }

            return Task.FromResult<DemoAuthSession?>(null);
        }

        public async Task<bool> IsAuthorizedAsync(string sessionId, UserRole requiredRole)
        {
            var session = await GetSessionAsync(sessionId);
            if (session == null)
            {
                return false;
            }

            // Admin has access to everything. Dispatchers can perform dispatcher-level routing.
            if (session.Role == UserRole.Admin)
            {
                return true;
            }

            return requiredRole switch
            {
                UserRole.Dispatcher => session.Role == UserRole.Dispatcher,
                UserRole.DepartmentResponder => session.Role == UserRole.Dispatcher || session.Role == UserRole.DepartmentResponder,
                UserRole.Viewer => true,
                _ => session.Role == requiredRole
            };
        }

        public Task<DemoUser?> GetUserByEmailAsync(string email)
        {
            var user = _users.FirstOrDefault(u => 
                u.Email.Equals(email, StringComparison.OrdinalIgnoreCase));
            return Task.FromResult(user);
        }

        private void CleanupExpiredSessions()
        {
            var expiredSessions = _sessions
                .Where(kvp => kvp.Value.ExpiresAt <= DateTime.UtcNow)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var sessionId in expiredSessions)
            {
                _sessions.Remove(sessionId);
            }
        }
    }
}
