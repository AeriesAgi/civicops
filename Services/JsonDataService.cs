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

            await EnsureRichSyntheticDataAsync();
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

        private async Task EnsureRichSyntheticDataAsync()
        {
            foreach (var incident in _incidents)
            {
                if (string.IsNullOrWhiteSpace(incident.RawDescription)) incident.RawDescription = incident.Description;
                if (string.IsNullOrWhiteSpace(incident.CleanedDescription)) incident.CleanedDescription = incident.Description;
                if (string.IsNullOrWhiteSpace(incident.OriginalArea)) incident.OriginalArea = incident.Suburb;
                if (string.IsNullOrWhiteSpace(incident.NormalizedArea)) incident.NormalizedArea = incident.Suburb;
                if (string.IsNullOrWhiteSpace(incident.Municipality)) incident.Municipality = "eThekwini";
                if (string.IsNullOrWhiteSpace(incident.WardConfidence)) incident.WardConfidence = incident.Ward.Contains("Needs", StringComparison.OrdinalIgnoreCase) ? "Low" : "Synthetic estimate";
                if (string.IsNullOrWhiteSpace(incident.EnrichmentSource)) incident.EnrichmentSource = incident.ClassificationMethod == "Deterministic" ? "Local deterministic fallback" : incident.ClassificationMethod;
                if (string.IsNullOrWhiteSpace(incident.RoutingConfidence)) incident.RoutingConfidence = "Synthetic demo routing";
                if (string.IsNullOrWhiteSpace(incident.RoutingReason)) incident.RoutingReason = $"Synthetic scenario routed to {incident.AssignedDepartment.GetDisplayName()} for {incident.Category}.";
                if (string.IsNullOrWhiteSpace(incident.AlertRecommendation)) incident.AlertRecommendation = incident.Priority == IncidentPriority.Urgent ? "Review for Area Alerts and dispatcher escalation." : "Ticket-level update recommended.";
                if (string.IsNullOrWhiteSpace(incident.CitizenResponse)) incident.CitizenResponse = $"Reference {incident.ReferenceNumber}: {incident.NormalizedArea}, {incident.Ward}. Routed to {incident.AssignedDepartment.GetDisplayName()}.";
                if (incident.PublicUpdates.Count == 0) incident.PublicUpdates.Add(new PublicUpdate { Content = incident.CitizenResponse, UpdatedBy = "CivicOps Demo Desk", RelatedStatus = incident.Status, Timestamp = incident.CreatedAt.AddMinutes(30) });
                if (incident.InternalNotes.Count == 0) incident.InternalNotes.Add(new IncidentNote { Content = $"Synthetic eThekwini scenario data. {incident.RoutingReason}", Author = "CivicOps Seed", Timestamp = incident.CreatedAt.AddMinutes(10) });
            }

            if (_incidents.Count < 100)
            {
                var areas = new[]
                {
                    ("Durban CBD", "Ward 26"), ("Chatsworth", "Ward 73"), ("Umlazi", "Ward 80"), ("Phoenix", "Ward 52"),
                    ("Pinetown", "Ward 23"), ("Amanzimtoti", "Ward 97"), ("KwaMashu", "Ward 55"), ("Westville", "Ward 24"),
                    ("Newlands", "Ward 31"), ("Inanda", "Ward 57"), ("Isipingo", "Ward 68"), ("Bluff", "Ward 66"),
                    ("Berea", "Ward 31"), ("Glenwood", "Ward 33"), ("Hillcrest", "Ward 8"), ("Reservoir Hills", "Ward 23"),
                    ("Tongaat", "Ward 61"), ("Verulam", "Ward 58"), ("Clermont", "Ward 92"), ("Cato Manor", "Ward 28"), ("Mobeni", "Ward 75"),
                    ("Merebank", "Ward 70"), ("Montclair", "Ward 64"), ("Queensburgh", "Ward 65"), ("Malvern", "Ward 18"),
                    ("Umhlanga", "Ward 35"), ("La Lucia", "Ward 36")
                };
                var scenarios = new[]
                {
                    ("burst pipe flooding the verge", "Water Infrastructure", Department.WaterAndSanitation, IncidentPriority.High),
                    ("water outage affecting several homes", "Water Infrastructure", Department.WaterAndSanitation, IncidentPriority.High),
                    ("blocked drain causing pooling after rain", "Stormwater", Department.RoadsAndStormwater, IncidentPriority.Medium),
                    ("large pothole damaging taxis", "Road Maintenance", Department.RoadsAndStormwater, IncidentPriority.Medium),
                    ("streetlight outage near school", "Street Lighting", Department.Electricity, IncidentPriority.High),
                    ("illegal dumping on vacant land", "Illegal Dumping", Department.WasteManagement, IncidentPriority.Medium),
                    ("missed refuse collection and overflowing bins", "Waste Collection", Department.WasteManagement, IncidentPriority.Medium),
                    ("fallen tree blocking pavement", "Parks", Department.ParksAndPublicSpaces, IncidentPriority.Medium),
                    ("traffic light failure at busy intersection", "Road Maintenance", Department.RoadsAndStormwater, IncidentPriority.High),
                    ("flooding risk at low bridge", "Disaster", Department.DisasterManagement, IncidentPriority.Urgent),
                    ("fire risk from exposed illegal burning", "Fire Safety", Department.FireAndRescue, IncidentPriority.Urgent),
                    ("informal settlement sanitation issue", "Informal Settlement", Department.HousingInformalSettlements, IncidentPriority.High),
                    ("public safety referral near commuter stop", "Public Safety", Department.MetroPolicePublicSafety, IncidentPriority.High),
                    ("environmental health complaint about smell", "Environmental Hazard", Department.EnvironmentalHealth, IncidentPriority.Medium),
                    ("ward committee follow-up needed for unclear location", "General Inquiry", Department.WardCouncillorWardCommittee, IncidentPriority.Low)
                };
                var statuses = new[] { IncidentStatus.New, IncidentStatus.Triaged, IncidentStatus.InProgress, IncidentStatus.Escalated, IncidentStatus.Resolved, IncidentStatus.Closed };
                var sources = new[] { SourceChannel.Web, SourceChannel.Android, SourceChannel.Demo, SourceChannel.VoiceNote, SourceChannel.WhatsApp };
                var next = _incidents.Select(i => int.TryParse(i.ReferenceNumber.Split('-').LastOrDefault(), out var n) ? n : 0).DefaultIfEmpty(0).Max() + 1;
                while (_incidents.Count < 108)
                {
                    var index = _incidents.Count;
                    var area = areas[index % areas.Length];
                    var scenario = scenarios[index % scenarios.Length];
                    var status = statuses[index % statuses.Length];
                    var created = DateTime.UtcNow.AddHours(-2 - index * 3);
                    var reference = $"CIV-2026-{next:D4}";
                    next++;
                    var incident = new Incident
                    {
                        ReferenceNumber = reference,
                        SourceChannel = sources[index % sources.Length],
                        Description = $"{scenario.Item1} in {area.Item1}",
                        RawDescription = $"{scenario.Item1} in {area.Item1}",
                        CleanedDescription = $"{scenario.Item1} in {area.Item1}",
                        AISummary = $"{scenario.Item2}: {scenario.Item1} reported in {area.Item1}.",
                        Category = scenario.Item2,
                        AssignedDepartment = scenario.Item3,
                        Suburb = area.Item1,
                        OriginalArea = area.Item1,
                        NormalizedArea = area.Item1,
                        Municipality = "eThekwini",
                        Ward = area.Item2,
                        WardConfidence = "Synthetic estimate",
                        Status = status,
                        Priority = scenario.Item4,
                        CreatedAt = created,
                        LastUpdatedAt = created.AddHours(1),
                        ClassificationMethod = index % 4 == 0 ? "Gemini/fallback civic AI (synthetic recommendation)" : "Local deterministic fallback",
                        EnrichmentSource = index % 4 == 0 ? "Gemini/fallback civic AI (synthetic recommendation)" : "Local deterministic fallback",
                        EnrichmentNotes = "Synthetic eThekwini scenario with normalized area and approximate ward for demo only.",
                        RoutingConfidence = "Synthetic demo routing",
                        RoutingReason = $"Synthetic {scenario.Item2.ToLowerInvariant()} scenario routed to {scenario.Item3.GetDisplayName()} for queue demonstration.",
                        AlertRecommendation = scenario.Item4 == IncidentPriority.Urgent ? "Review for Area Alert and escalation." : "Keep public ticket updates active."
                    };
                    incident.CitizenResponse = $"Reference {reference}: Area recorded as {area.Item1}. Ward estimate: {area.Item2}. Routed to {scenario.Item3.GetDisplayName()}.";
                    incident.PublicUpdates.Add(new PublicUpdate { Content = "Ticket received and routed for staff review.", UpdatedBy = "CivicOps Demo Desk", RelatedStatus = IncidentStatus.Triaged, Timestamp = created.AddMinutes(20) });
                    if (status == IncidentStatus.InProgress || status == IncidentStatus.Resolved || status == IncidentStatus.Closed)
                    {
                        incident.PublicUpdates.Add(new PublicUpdate { Content = "Department queue accepted the ticket and posted an update.", UpdatedBy = scenario.Item3.GetDisplayName(), RelatedStatus = status, Timestamp = created.AddHours(1) });
                    }
                    incident.InternalNotes.Add(new IncidentNote { Content = $"Synthetic audit: {incident.RoutingReason} No live municipal data claimed.", Author = "CivicOps Seed", Timestamp = created.AddMinutes(10) });
                    _incidents.Add(incident);
                }
                _referenceCounter = next;
            }


            if (_alerts.Count < 18)
            {
                var alertScenarios = new[]
                {
                    (AlertType.WaterOutage, AlertSeverity.Warning, "Water pressure monitoring", "Low pressure reported across parts of the area; crews are validating valve status.", "Chatsworth", "Ward 73", Department.WaterAndSanitation),
                    (AlertType.ElectricityDisruption, AlertSeverity.Urgent, "Unplanned feeder outage", "Electricity disruption affecting pockets of Phoenix; restoration estimate pending field confirmation.", "Phoenix", "Ward 52", Department.Electricity),
                    (AlertType.RoadClosure, AlertSeverity.Warning, "Stormwater road caution", "Standing water and blocked drains reported near low-lying crossings. Drive with caution.", "Isipingo", "Ward 68", Department.RoadsAndStormwater),
                    (AlertType.WasteCollectionDisruption, AlertSeverity.Info, "Refuse route delay", "Collection teams are running behind schedule; keep bins accessible until the route is cleared.", "Umlazi", "Ward 80", Department.WasteManagement),
                    (AlertType.PublicSafetyNotice, AlertSeverity.Warning, "Commuter corridor safety notice", "Metro safety teams are monitoring repeated reports near evening commuter points.", "Durban CBD", "Ward 26", Department.MetroPolicePublicSafety),
                    (AlertType.Flood, AlertSeverity.Urgent, "Heavy rain runoff watch", "Rainfall may affect roads, informal drainage channels and low bridges; avoid crossing flooded roads.", "Amanzimtoti", "Ward 97", Department.DisasterManagement),
                    (AlertType.EnvironmentalHazard, AlertSeverity.Warning, "Illegal dumping hotspot", "Repeat dumping reports are being reviewed for cleanup and enforcement response.", "Clermont", "Ward 92", Department.WasteManagement),
                    (AlertType.Fire, AlertSeverity.Critical, "Informal burning risk", "Open burning reports near dense housing require urgent safety review and public caution.", "KwaMashu", "Ward 55", Department.FireAndRescue),
                    (AlertType.DisasterWarning, AlertSeverity.Warning, "Weather disruption advisory", "High winds and localized storms may affect trees, power lines and road visibility.", "Pinetown", "Ward 23", Department.DisasterManagement),
                    (AlertType.RoadClosure, AlertSeverity.Info, "Planned resurfacing", "Night works scheduled on selected lanes; expect delays and follow temporary signage.", "Bluff", "Ward 66", Department.RoadsAndStormwater)
                };

                var existingKeys = _alerts.Select(a => $"{a.Title}|{a.Suburb}").ToHashSet(StringComparer.OrdinalIgnoreCase);
                foreach (var scenario in alertScenarios)
                {
                    if (existingKeys.Contains($"{scenario.Item3}|{scenario.Item5}")) continue;
                    _alerts.Add(new Alert
                    {
                        Type = scenario.Item1,
                        Severity = scenario.Item2,
                        Title = scenario.Item3,
                        Description = scenario.Item4,
                        Suburb = scenario.Item5,
                        Ward = scenario.Item6,
                        AffectedDepartment = scenario.Item7,
                        CreatedAt = DateTime.UtcNow.AddHours(-2 - _alerts.Count),
                        ExpiresAt = DateTime.UtcNow.AddDays(2)
                    });
                }

                await SaveAlertsAsync();
            }

            await SaveIncidentsAsync();
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
                    PublicUpdates = new List<PublicUpdate> 
                    { 
                        new PublicUpdate { Content = "Crew dispatched to site", UpdatedBy = "Dispatcher" },
                        new PublicUpdate { Content = "Repair work in progress", UpdatedBy = "Field Crew" }
                    },
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
                    PublicUpdates = new List<PublicUpdate> 
                    { 
                        new PublicUpdate { Content = "Investigating cause of outage", UpdatedBy = "Dispatcher" }
                    },
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
                    PublicUpdates = new List<PublicUpdate> 
                    { 
                        new PublicUpdate { Content = "Fire safety inspection scheduled", UpdatedBy = "Fire Inspector" },
                        new PublicUpdate { Content = "Escalated to disaster management", UpdatedBy = "Admin" }
                    },
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
                    PublicUpdates = new List<PublicUpdate> 
                    { 
                        new PublicUpdate { Content = "Emergency crew dispatched", UpdatedBy = "Dispatcher" }
                    },
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
                    PublicUpdates = new List<PublicUpdate> 
                    { 
                        new PublicUpdate { Content = "Catch-up collection scheduled", UpdatedBy = "Waste Manager" },
                        new PublicUpdate { Content = "Collection completed", UpdatedBy = "Collection Crew" }
                    },
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
                    PublicUpdates = new List<PublicUpdate> 
                    { 
                        new PublicUpdate { Content = "Patrol dispatched to area", UpdatedBy = "Metro Police" }
                    },
                    ClassificationMethod = "Deterministic"
                },
                new Incident
                {
                    ReferenceNumber = "CIV-2026-0011",
                    SourceChannel = SourceChannel.Android,
                    Description = "Street light not working on corner of Main and 5th for over a week",
                    AISummary = "Street lighting maintenance required in Morningside.",
                    Category = "Street Lighting",
                    AssignedDepartment = Department.Electricity,
                    Suburb = "Morningside",
                    Ward = "Ward 33",
                    Status = IncidentStatus.Assigned,
                    Priority = IncidentPriority.Low,
                    CreatedAt = DateTime.UtcNow.AddDays(-2),
                    LastUpdatedAt = DateTime.UtcNow.AddDays(-1),
                    ClassificationMethod = "Deterministic"
                },
                new Incident
                {
                    ReferenceNumber = "CIV-2026-0012",
                    SourceChannel = SourceChannel.Web,
                    Description = "Manhole cover missing on busy road, very dangerous",
                    AISummary = "Urgent road safety hazard - missing manhole cover.",
                    Category = "Road Safety",
                    AssignedDepartment = Department.RoadsAndStormwater,
                    Suburb = "Westville",
                    Ward = "Ward 45",
                    Status = IncidentStatus.Escalated,
                    Priority = IncidentPriority.Urgent,
                    CreatedAt = DateTime.UtcNow.AddHours(-4),
                    LastUpdatedAt = DateTime.UtcNow.AddHours(-3),
                    PublicUpdates = new List<PublicUpdate> 
                    { 
                        new PublicUpdate { Content = "Emergency crew dispatched", UpdatedBy = "Dispatcher" },
                        new PublicUpdate { Content = "Temporary barriers placed, repair scheduled", UpdatedBy = "Roads Crew" }
                    },
                    ClassificationMethod = "Deterministic"
                },
                new Incident
                {
                    ReferenceNumber = "CIV-2026-0013",
                    SourceChannel = SourceChannel.VoiceNote,
                    Description = "Water pressure very low in our area since yesterday",
                    AISummary = "Low water pressure reported in Glenwood area.",
                    Category = "Water Supply",
                    AssignedDepartment = Department.WaterAndSanitation,
                    Suburb = "Glenwood",
                    Ward = "Ward 29",
                    Status = IncidentStatus.InProgress,
                    Priority = IncidentPriority.Medium,
                    CreatedAt = DateTime.UtcNow.AddHours(-18),
                    LastUpdatedAt = DateTime.UtcNow.AddHours(-12),
                    AudioMetadata = "voice-note-water-pressure.mp3",
                    PublicUpdates = new List<PublicUpdate> 
                    { 
                        new PublicUpdate { Content = "Investigating cause of low pressure", UpdatedBy = "Water Department" }
                    },
                    ClassificationMethod = "Deterministic"
                },
                new Incident
                {
                    ReferenceNumber = "CIV-2026-0014",
                    SourceChannel = SourceChannel.WhatsApp,
                    Description = "Tree fallen across road after storm, blocking traffic",
                    AISummary = "Storm damage - fallen tree blocking road in Kloof.",
                    Category = "Storm Damage",
                    AssignedDepartment = Department.ParksAndPublicSpaces,
                    Suburb = "Kloof",
                    Ward = "Ward 12",
                    Status = IncidentStatus.Resolved,
                    Priority = IncidentPriority.High,
                    CreatedAt = DateTime.UtcNow.AddHours(-8),
                    LastUpdatedAt = DateTime.UtcNow.AddHours(-5),
                    PublicUpdates = new List<PublicUpdate> 
                    { 
                        new PublicUpdate { Content = "Emergency tree removal crew dispatched", UpdatedBy = "Dispatcher" },
                        new PublicUpdate { Content = "Tree removed, road cleared", UpdatedBy = "Parks Crew" }
                    },
                    ClassificationMethod = "Deterministic"
                },
                new Incident
                {
                    ReferenceNumber = "CIV-2026-0015",
                    SourceChannel = SourceChannel.Android,
                    Description = "Graffiti on public building and bus shelter",
                    AISummary = "Vandalism reported - graffiti on public property in Berea.",
                    Category = "Vandalism",
                    AssignedDepartment = Department.ParksAndPublicSpaces,
                    Suburb = "Berea",
                    Ward = "Ward 25",
                    Status = IncidentStatus.New,
                    Priority = IncidentPriority.Low,
                    CreatedAt = DateTime.UtcNow.AddHours(-6),
                    LastUpdatedAt = DateTime.UtcNow.AddHours(-6),
                    ClassificationMethod = "Deterministic"
                },
                new Incident
                {
                    ReferenceNumber = "CIV-2026-0016",
                    SourceChannel = SourceChannel.Web,
                    Description = "Stray dogs in residential area, concerned about safety",
                    AISummary = "Animal control needed - stray dogs in Reservoir Hills.",
                    Category = "Animal Control",
                    AssignedDepartment = Department.EnvironmentalHealth,
                    Suburb = "Reservoir Hills",
                    Ward = "Ward 27",
                    Status = IncidentStatus.Triaged,
                    Priority = IncidentPriority.Medium,
                    CreatedAt = DateTime.UtcNow.AddHours(-12),
                    LastUpdatedAt = DateTime.UtcNow.AddHours(-10),
                    ClassificationMethod = "Deterministic"
                },
                new Incident
                {
                    ReferenceNumber = "CIV-2026-0017",
                    SourceChannel = SourceChannel.WhatsApp,
                    Description = "Noise complaint - construction work starting at 5am",
                    AISummary = "Noise pollution complaint in Umhlanga.",
                    Category = "Noise Pollution",
                    AssignedDepartment = Department.EnvironmentalHealth,
                    Suburb = "Umhlanga",
                    Ward = "Ward 35",
                    Status = IncidentStatus.Assigned,
                    Priority = IncidentPriority.Low,
                    CreatedAt = DateTime.UtcNow.AddDays(-1),
                    LastUpdatedAt = DateTime.UtcNow.AddHours(-20),
                    PublicUpdates = new List<PublicUpdate> 
                    { 
                        new PublicUpdate { Content = "Inspector assigned to investigate", UpdatedBy = "Health Department" }
                    },
                    ClassificationMethod = "Deterministic"
                },
                new Incident
                {
                    ReferenceNumber = "CIV-2026-0018",
                    SourceChannel = SourceChannel.Demo,
                    Description = "Flooding in low-lying area after heavy rain",
                    AISummary = "Flood response needed in Cato Manor.",
                    Category = "Flood",
                    AssignedDepartment = Department.DisasterManagement,
                    Suburb = "Cato Manor",
                    Ward = "Ward 28",
                    Status = IncidentStatus.Escalated,
                    Priority = IncidentPriority.Urgent,
                    CreatedAt = DateTime.UtcNow.AddHours(-3),
                    LastUpdatedAt = DateTime.UtcNow.AddHours(-2),
                    PublicUpdates = new List<PublicUpdate> 
                    { 
                        new PublicUpdate { Content = "Disaster management team activated", UpdatedBy = "Admin" },
                        new PublicUpdate { Content = "Evacuation assistance available", UpdatedBy = "Disaster Management" }
                    },
                    ClassificationMethod = "Deterministic"
                }
            };

            _referenceCounter = 19;
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
                },
                new Alert
                {
                    Type = AlertType.Flood,
                    Severity = AlertSeverity.Urgent,
                    Title = "Flood Warning - Heavy Rain Expected",
                    Description = "Heavy rainfall expected. Low-lying areas may experience flooding. Residents advised to take precautions.",
                    Suburb = "Cato Manor",
                    Ward = "Ward 28",
                    AffectedDepartment = Department.DisasterManagement,
                    CreatedAt = DateTime.UtcNow.AddHours(-4),
                    ExpiresAt = DateTime.UtcNow.AddHours(20)
                },
                new Alert
                {
                    Type = AlertType.EnvironmentalHazard,
                    Severity = AlertSeverity.Warning,
                    Title = "Air Quality Advisory",
                    Description = "Elevated air pollution levels due to industrial activity. Sensitive individuals should limit outdoor exposure.",
                    Suburb = "Merebank",
                    Ward = "Ward 70",
                    AffectedDepartment = Department.EnvironmentalHealth,
                    CreatedAt = DateTime.UtcNow.AddHours(-36),
                    ExpiresAt = DateTime.UtcNow.AddHours(12)
                },
                new Alert
                {
                    Type = AlertType.DisasterWarning,
                    Severity = AlertSeverity.Critical,
                    Title = "Severe Weather Warning",
                    Description = "Severe thunderstorms and strong winds expected this evening. Secure loose objects and stay indoors.",
                    Suburb = "All Areas",
                    Ward = "All Wards",
                    AffectedDepartment = Department.DisasterManagement,
                    CreatedAt = DateTime.UtcNow.AddHours(-2),
                    ExpiresAt = DateTime.UtcNow.AddHours(8)
                }
            };

            _alerts.AddRange(demoAlerts);
            await SaveAlertsAsync();
        }
    }
}
