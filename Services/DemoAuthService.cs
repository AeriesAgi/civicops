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
                    DisplayName = "Roads Department",
                    Role = UserRole.DepartmentResponder,
                    AssignedDepartment = Department.RoadsAndStormwater,
                    IsActive = true
                }
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

            // Admin has access to everything
            if (session.Role == UserRole.Admin)
            {
                return true;
            }

            // Check role hierarchy
            return session.Role >= requiredRole;
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
