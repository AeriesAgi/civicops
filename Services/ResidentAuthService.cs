using CivicOps.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace CivicOps.Services
{
    public class ResidentAuthService : IResidentAuthService
    {
        private readonly string _dataFilePath;
        private List<ResidentUser> _users = new();
        private readonly object _lock = new();

        public ResidentAuthService()
        {
            var dataDir = Path.Combine(Directory.GetCurrentDirectory(), "Data");
            Directory.CreateDirectory(dataDir);
            _dataFilePath = Path.Combine(dataDir, "resident_users.json");
        }

        public async Task InitializeAsync()
        {
            if (File.Exists(_dataFilePath))
            {
                var json = await File.ReadAllTextAsync(_dataFilePath);
                _users = JsonSerializer.Deserialize<List<ResidentUser>>(json) ?? new();
            }
            else
            {
                // Seed demo resident users
                _users = new List<ResidentUser>
                {
                    new ResidentUser
                    {
                        Email = "resident@civicops.demo",
                        Password = "CivicOps2026!",
                        FullName = "Demo Resident",
                        PhoneNumber = "+27 82 123 4567",
                        FollowedSuburbs = new List<string> { "Chatsworth", "Umlazi" },
                        FollowedWards = new List<string> { "Ward 68", "Ward 80" },
                        SubmittedReportReferences = new List<string> { "CIV-2026-0001", "CIV-2026-0005" }
                    },
                    new ResidentUser
                    {
                        Email = "john.smith@example.com",
                        Password = "Demo2026!",
                        FullName = "John Smith",
                        PhoneNumber = "+27 83 456 7890",
                        FollowedSuburbs = new List<string> { "Phoenix", "Durban CBD" },
                        FollowedWards = new List<string> { "Ward 25" },
                        SubmittedReportReferences = new List<string> { "CIV-2026-0003" }
                    },
                    new ResidentUser
                    {
                        Email = "sarah.jones@example.com",
                        Password = "Demo2026!",
                        FullName = "Sarah Jones",
                        PhoneNumber = "+27 84 789 0123",
                        FollowedSuburbs = new List<string> { "Khayelitsha", "Mitchells Plain" },
                        FollowedWards = new List<string> { "Ward 87", "Ward 92" },
                        SubmittedReportReferences = new List<string>()
                    }
                };
                await SaveAsync();
            }
        }

        public Task<ResidentUser?> AuthenticateAsync(string email, string password)
        {
            lock (_lock)
            {
                var user = _users.FirstOrDefault(u => 
                    u.Email.Equals(email, StringComparison.OrdinalIgnoreCase) && 
                    u.Password == password);
                
                if (user != null)
                {
                    user.LastLoginAt = DateTime.UtcNow;
                    _ = SaveAsync(); // Fire and forget
                }
                
                return Task.FromResult(user);
            }
        }

        public Task<ResidentUser?> GetUserByEmailAsync(string email)
        {
            lock (_lock)
            {
                var user = _users.FirstOrDefault(u => 
                    u.Email.Equals(email, StringComparison.OrdinalIgnoreCase));
                return Task.FromResult(user);
            }
        }

        public Task<ResidentUser?> GetUserByIdAsync(string userId)
        {
            lock (_lock)
            {
                var user = _users.FirstOrDefault(u => u.Id == userId);
                return Task.FromResult(user);
            }
        }

        public async Task<ResidentUser> CreateUserAsync(string email, string password, string fullName, string? phoneNumber = null)
        {
            lock (_lock)
            {
                // Check if user already exists
                if (_users.Any(u => u.Email.Equals(email, StringComparison.OrdinalIgnoreCase)))
                {
                    throw new InvalidOperationException("User with this email already exists");
                }

                var user = new ResidentUser
                {
                    Email = email,
                    Password = password, // Demo only - plain text
                    FullName = fullName,
                    PhoneNumber = phoneNumber,
                    CreatedAt = DateTime.UtcNow,
                    LastLoginAt = DateTime.UtcNow
                };

                _users.Add(user);
                _ = SaveAsync(); // Fire and forget
                return user;
            }
        }

        public async Task UpdateUserAsync(ResidentUser user)
        {
            lock (_lock)
            {
                var existingUser = _users.FirstOrDefault(u => u.Id == user.Id);
                if (existingUser != null)
                {
                    var index = _users.IndexOf(existingUser);
                    _users[index] = user;
                }
            }
            await SaveAsync();
        }

        public async Task AddReportReferenceAsync(string userId, string referenceNumber)
        {
            lock (_lock)
            {
                var user = _users.FirstOrDefault(u => u.Id == userId);
                if (user != null && !user.SubmittedReportReferences.Contains(referenceNumber))
                {
                    user.SubmittedReportReferences.Add(referenceNumber);
                }
            }
            await SaveAsync();
        }

        public async Task AddFollowedSuburbAsync(string userId, string suburb)
        {
            lock (_lock)
            {
                var user = _users.FirstOrDefault(u => u.Id == userId);
                if (user != null && !user.FollowedSuburbs.Contains(suburb))
                {
                    user.FollowedSuburbs.Add(suburb);
                }
            }
            await SaveAsync();
        }

        public async Task RemoveFollowedSuburbAsync(string userId, string suburb)
        {
            lock (_lock)
            {
                var user = _users.FirstOrDefault(u => u.Id == userId);
                if (user != null)
                {
                    user.FollowedSuburbs.Remove(suburb);
                }
            }
            await SaveAsync();
        }

        public async Task AddFollowedWardAsync(string userId, string ward)
        {
            lock (_lock)
            {
                var user = _users.FirstOrDefault(u => u.Id == userId);
                if (user != null && !user.FollowedWards.Contains(ward))
                {
                    user.FollowedWards.Add(ward);
                }
            }
            await SaveAsync();
        }

        public async Task RemoveFollowedWardAsync(string userId, string ward)
        {
            lock (_lock)
            {
                var user = _users.FirstOrDefault(u => u.Id == userId);
                if (user != null)
                {
                    user.FollowedWards.Remove(ward);
                }
            }
            await SaveAsync();
        }

        private async Task SaveAsync()
        {
            var json = JsonSerializer.Serialize(_users, new JsonSerializerOptions 
            { 
                WriteIndented = true 
            });
            await File.WriteAllTextAsync(_dataFilePath, json);
        }
    }
}
