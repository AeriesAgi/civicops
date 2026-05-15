using CivicOps.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace CivicOps.Services
{
    public class JsonDataService : IDataService
    {
        private readonly string _dataPath;
        private readonly string _incidentsFile;
        private readonly string _alertsFile;
        private List<Incident> _incidents = new();
        private List<Alert> _alerts = new();
        private int _referenceCounter = 1;

        public JsonDataService()
        {
            _dataPath = Path.Combine(Directory.GetCurrentDirectory(), "Data");
            _incidentsFile = Path.Combine(_dataPath, "incidents.json");
            _alertsFile = Path.Combine(_dataPath, "alerts.json");
        }

        public async Task InitializeAsync()
        {
            if (!Directory.Exists(_dataPath))
            {
                Directory.CreateDirectory(_dataPath);
            }

            await LoadDataAsync();
            
            if (_incidents.Count == 0)
            {
                await SeedDemoDataAsync();
            }
        }

        private async Task LoadDataAsync()
        {
            try
            {
                if (File.Exists(_incidentsFile))
                {
                    var json = await File.ReadAllTextAsync(_incidentsFile);
                    _incidents = JsonSerializer.Deserialize<List<Incident>>(json) ?? new();
                    
                    // Update reference counter
                    if (_incidents.Any())
                    {
                        var maxRef = _incidents
                            .Select(i => i.ReferenceNumber)
                            .Where(r => r.StartsWith("CIV-2026-"))
                            .Select(r => int.TryParse(r.Split('-').Last(), out var num) ? num : 0)
                            .DefaultIfEmpty(0)
                            .Max();
                        _referenceCounter = maxRef + 1;
                    }
                }

                if (File.Exists(_alertsFile))
                {
                    var json = await File.ReadAllTextAsync(_alertsFile);
                    _alerts = JsonSerializer.Deserialize<List<Alert>>(json) ?? new();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading data: {ex.Message}");
            }
        }

        private async Task SaveIncidentsAsync()
        {
            var json = JsonSerializer.Serialize(_incidents, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_incidentsFile, json);
        }

        private async Task SaveAlertsAsync()
        {
            var json = JsonSerializer.Serialize(_alerts, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_alertsFile, json);
        }

        public Task<List<Incident>> GetAllIncidentsAsync()
        {
            return Task.FromResult(_incidents.OrderByDescending(i => i.CreatedAt).ToList());
        }

        public Task<Incident?> GetIncidentByIdAsync(string id)
        {
            return Task.FromResult(_incidents.FirstOrDefault(i => i.Id == id));
        }

        public Task<Incident?> GetIncidentByReferenceAsync(string reference)
        {
            return Task.FromResult(_incidents.FirstOrDefault(i => i.ReferenceNumber == reference));
        }

        public Task<List<Incident>> GetIncidentsByDepartmentAsync(Department department)
        {
            return Task.FromResult(_incidents
                .Where(i => i.AssignedDepartment == department)
                .OrderByDescending(i => i.CreatedAt)
                .ToList());
        }

        public async Task<string> SaveIncidentAsync(Incident incident)
        {
            if (string.IsNullOrEmpty(incident.ReferenceNumber))
            {
                incident.ReferenceNumber = await GenerateReferenceNumberAsync();
            }
            
            _incidents.Add(incident);
            await SaveIncidentsAsync();
            return incident.Id;
        }

        public async Task UpdateIncidentAsync(Incident incident)
        {
            var existing = _incidents.FirstOrDefault(i => i.Id == incident.Id);
            if (existing != null)
            {
                var index = _incidents.IndexOf(existing);
                incident.LastUpdatedAt = DateTime.UtcNow;
                _incidents[index] = incident;
                await SaveIncidentsAsync();
            }
        }

        public Task<List<Alert>> GetAllAlertsAsync()
        {
            return Task.FromResult(_alerts
                .Where(a => a.IsActive)
                .OrderByDescending(a => a.CreatedAt)
                .ToList());
        }

        public Task<List<Alert>> GetAlertsByAreaAsync(string? suburb = null, string? ward = null)
        {
            var query = _alerts.Where(a => a.IsActive);
            
            if (!string.IsNullOrEmpty(suburb))
            {
                query = query.Where(a => a.Suburb.Equals(suburb, StringComparison.OrdinalIgnoreCase));
            }
            
            if (!string.IsNullOrEmpty(ward))
            {
                query = query.Where(a => a.Ward.Equals(ward, StringComparison.OrdinalIgnoreCase));
            }
            
            return Task.FromResult(query.OrderByDescending(a => a.CreatedAt).ToList());
        }

        public async Task<string> SaveAlertAsync(Alert alert)
        {
            _alerts.Add(alert);
            await SaveAlertsAsync();
            return alert.Id;
        }

        public Task<string> GenerateReferenceNumberAsync()
        {
            var reference = $"CIV-2026-{_referenceCounter:D4}";
            _referenceCounter++;
            return Task.FromResult(reference);
        }

        private async Task SeedDemoDataAsync()
        {
            // Seed demo incidents
            var demoIncidents = new List<Incident>
            {
                new Incident
                {
                    ReferenceNumber = "CIV-2026-0001",
                    SourceChannel = SourceChannel.Web,
                    Description = "Burst water pipe on Main Road causing flooding",
                    AISummary = "Water pipe burst on Main Road, Chatsworth. Flooding reported.",
                    Category = "Water Infrastructure",
                    AssignedDepartment = Department.WaterAndSanitation,
                    Suburb = "Chatsworth",
                    Ward = "Ward 73",
                    Status = IncidentStatus.InProgress,
                    Priority = IncidentPriority.High,
                    CreatedAt = DateTime.UtcNow.AddHours(-5),
                    LastUpdatedAt = DateTime.UtcNow.AddHours(-2),
                    PublicUpdates = new List<string> { "Crew dispatched to site", "Repair work in progress" },
                    ClassificationMethod = "Deterministic"
                },
                new Incident
                {
                    ReferenceNumber = "CIV-2026-0002",
                    SourceChannel = SourceChannel.WhatsApp,
                    Description = "Power outage in our area since this morning",
                    AISummary = "Electricity outage reported in Umlazi area.",
                    Category = "Electricity",
                    AssignedDepartment = Department.Electricity,
                    Suburb = "Umlazi",
                    Ward = "Ward 80",
                    Status = IncidentStatus.Assigned,
                    Priority = IncidentPriority.High,
                    CreatedAt = DateTime.UtcNow.AddHours(-3),
                    LastUpdatedAt = DateTime.UtcNow.AddHours(-1),
                    PublicUpdates = new List<string> { "Investigating cause of outage" },
                    ClassificationMethod = "Deterministic"
                },
                new Incident
                {
                    ReferenceNumber = "CIV-2026-0003",
                    SourceChannel = SourceChannel.Android,
                    Description = "Large pothole on Phoenix Highway near the bridge",
                    AISummary = "Pothole reported on Phoenix Highway.",
                    Category = "Road Maintenance",
                    AssignedDepartment = Department.RoadsAndStormwater,
                    Suburb = "Phoenix",
                    Ward = "Ward 52",
                    Status = IncidentStatus.New,
                    Priority = IncidentPriority.Medium,
                    CreatedAt = DateTime.UtcNow.AddHours(-1),
                    LastUpdatedAt = DateTime.UtcNow.AddHours(-1),
                    ClassificationMethod = "Deterministic"
                },
                new Incident
                {
                    ReferenceNumber = "CIV-2026-0004",
                    SourceChannel = SourceChannel.Web,
                    Description = "Illegal dumping of building rubble on vacant lot",
                    AISummary = "Illegal dumping reported in Pinetown.",
                    Category = "Waste Management",
                    AssignedDepartment = Department.WasteManagement,
                    Suburb = "Pinetown",
                    Ward = "Ward 23",
                    Status = IncidentStatus.Triaged,
                    Priority = IncidentPriority.Medium,
                    CreatedAt = DateTime.UtcNow.AddHours(-6),
                    LastUpdatedAt = DateTime.UtcNow.AddHours(-4),
                    ClassificationMethod = "Deterministic"
                },
                new Incident
                {
                    ReferenceNumber = "CIV-2026-0005",
                    SourceChannel = SourceChannel.VoiceNote,
                    Description = "Blocked storm drain causing water to pool on the street",
                    AISummary = "Storm drain blockage in Durban CBD.",
                    Category = "Stormwater",
                    AssignedDepartment = Department.RoadsAndStormwater,
                    Suburb = "Durban CBD",
                    Ward = "Ward 26",
                    Status = IncidentStatus.New,
                    Priority = IncidentPriority.Medium,
                    CreatedAt = DateTime.UtcNow.AddMinutes(-45),
                    LastUpdatedAt = DateTime.UtcNow.AddMinutes(-45),
                    AudioMetadata = "voice-note-placeholder.mp3",
                    ClassificationMethod = "Deterministic"
                },
                new Incident
                {
                    ReferenceNumber = "CIV-2026-0006",
                    SourceChannel = SourceChannel.Web,
                    Description = "Fire hazard - informal settlement shacks very close together",
                    AISummary = "Fire risk identified in informal settlement.",
                    Category = "Fire Safety",
                    AssignedDepartment = Department.FireAndRescue,
                    Suburb = "KwaMashu",
                    Ward = "Ward 55",
                    Status = IncidentStatus.Escalated,
                    Priority = IncidentPriority.Urgent,
                    CreatedAt = DateTime.UtcNow.AddHours(-8),
                    LastUpdatedAt = DateTime.UtcNow.AddHours(-7),
                    PublicUpdates = new List<string> { "Fire safety inspection scheduled", "Escalated to disaster management" },
                    ClassificationMethod = "Deterministic"
                },
                new Incident
                {
                    ReferenceNumber = "CIV-2026-0007",
                    SourceChannel = SourceChannel.WhatsApp,
                    Description = "Sewage leak on our street, very bad smell",
                    AISummary = "Sewage leak reported in Isipingo.",
                    Category = "Sanitation",
                    AssignedDepartment = Department.WaterAndSanitation,
                    Suburb = "Isipingo",
                    Ward = "Ward 68",
                    Status = IncidentStatus.InProgress,
                    Priority = IncidentPriority.High,
                    CreatedAt = DateTime.UtcNow.AddHours(-4),
                    LastUpdatedAt = DateTime.UtcNow.AddHours(-2),
                    PublicUpdates = new List<string> { "Emergency crew dispatched" },
                    ClassificationMethod = "Deterministic"
                },
                new Incident
                {
                    ReferenceNumber = "CIV-2026-0008",
                    SourceChannel = SourceChannel.Android,
                    Description = "Park equipment broken and dangerous for children",
                    AISummary = "Damaged playground equipment in Newlands park.",
                    Category = "Parks",
                    AssignedDepartment = Department.ParksAndPublicSpaces,
                    Suburb = "Newlands",
                    Ward = "Ward 31",
                    Status = IncidentStatus.Assigned,
                    Priority = IncidentPriority.Medium,
                    CreatedAt = DateTime.UtcNow.AddHours(-10),
                    LastUpdatedAt = DateTime.UtcNow.AddHours(-9),
                    ClassificationMethod = "Deterministic"
                },
                new Incident
                {
                    ReferenceNumber = "CIV-2026-0009",
                    SourceChannel = SourceChannel.Web,
                    Description = "Refuse not collected for 2 weeks, bins overflowing",
                    AISummary = "Waste collection service disruption in Hillcrest.",
                    Category = "Waste Collection",
                    AssignedDepartment = Department.WasteManagement,
                    Suburb = "Hillcrest",
                    Ward = "Ward 8",
                    Status = IncidentStatus.Resolved,
                    Priority = IncidentPriority.Medium,
                    CreatedAt = DateTime.UtcNow.AddDays(-3),
                    LastUpdatedAt = DateTime.UtcNow.AddDays(-1),
                    PublicUpdates = new List<string> { "Catch-up collection scheduled", "Collection completed" },
                    ClassificationMethod = "Deterministic"
                },
                new Incident
                {
                    ReferenceNumber = "CIV-2026-0010",
                    SourceChannel = SourceChannel.WhatsApp,
                    Description = "Suspicious activity near school, need metro police patrol",
                    AISummary = "Public safety concern near school in Bluff.",
                    Category = "Public Safety",
                    AssignedDepartment = Department.MetroPolicePublicSafety,
                    Suburb = "Bluff",
                    Ward = "Ward 66",
                    Status = IncidentStatus.InProgress,
                    Priority = IncidentPriority.High,
                    CreatedAt = DateTime.UtcNow.AddHours(-2),
                    LastUpdatedAt = DateTime.UtcNow.AddMinutes(-30),
                    PublicUpdates = new List<string> { "Patrol dispatched to area" },
                    ClassificationMethod = "Deterministic"
                }
            };

            _referenceCounter = 11;
            _incidents.AddRange(demoIncidents);
            await SaveIncidentsAsync();

            // Seed demo alerts
            var demoAlerts = new List<Alert>
            {
                new Alert
                {
                    Type = AlertType.WaterOutage,
                    Severity = AlertSeverity.Warning,
                    Title = "Planned Water Maintenance",
                    Description = "Water supply will be interrupted on Saturday 17 May from 08:00 to 16:00 for maintenance work.",
                    Suburb = "Chatsworth",
                    Ward = "Ward 73",
                    AffectedDepartment = Department.WaterAndSanitation,
                    CreatedAt = DateTime.UtcNow.AddHours(-12),
                    ExpiresAt = DateTime.UtcNow.AddDays(2)
                },
                new Alert
                {
                    Type = AlertType.ElectricityDisruption,
                    Severity = AlertSeverity.Urgent,
                    Title = "Load Shedding Stage 2",
                    Description = "Stage 2 load shedding in effect. Check schedule for affected times.",
                    Suburb = "Umlazi",
                    Ward = "Ward 80",
                    AffectedDepartment = Department.Electricity,
                    CreatedAt = DateTime.UtcNow.AddHours(-6)
                },
                new Alert
                {
                    Type = AlertType.RoadClosure,
                    Severity = AlertSeverity.Warning,
                    Title = "Road Closure - Phoenix Highway",
                    Description = "Phoenix Highway closed between 10:00-14:00 today for emergency repairs.",
                    Suburb = "Phoenix",
                    Ward = "Ward 52",
                    AffectedDepartment = Department.RoadsAndStormwater,
                    CreatedAt = DateTime.UtcNow.AddHours(-3),
                    ExpiresAt = DateTime.UtcNow.AddHours(2)
                },
                new Alert
                {
                    Type = AlertType.WasteCollectionDisruption,
                    Severity = AlertSeverity.Info,
                    Title = "Waste Collection Delay",
                    Description = "Waste collection delayed by one day this week due to public holiday.",
                    Suburb = "Hillcrest",
                    Ward = "Ward 8",
                    AffectedDepartment = Department.WasteManagement,
                    CreatedAt = DateTime.UtcNow.AddHours(-24),
                    ExpiresAt = DateTime.UtcNow.AddDays(1)
                },
                new Alert
                {
                    Type = AlertType.PublicSafetyNotice,
                    Severity = AlertSeverity.Warning,
                    Title = "Increased Patrols",
                    Description = "Metro Police increasing patrols in the area following recent incidents.",
                    Suburb = "Bluff",
                    Ward = "Ward 66",
                    AffectedDepartment = Department.MetroPolicePublicSafety,
                    CreatedAt = DateTime.UtcNow.AddHours(-18)
                }
            };

            _alerts.AddRange(demoAlerts);
            await SaveAlertsAsync();
        }
    }
}
