using CivicOps.Models;
using CivicOps.Services;
using CivicOps.Filters;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace CivicOps.Controllers
{
    public class HomeController : Controller
    {
        private readonly IDataService _dataService;
        private readonly IGeminiService _geminiService;
        private readonly IClassificationService _classificationService;
        private readonly IWeatherService _weatherService;
        private readonly IIncidentIntakeService _intakeService;
        private readonly IWhatsAppService _whatsAppService;
        private readonly IConfiguration _configuration;
        private readonly IDemoAuthService _demoAuthService;

        public HomeController(
            IDataService dataService,
            IGeminiService geminiService,
            IClassificationService classificationService,
            IWeatherService weatherService,
            IIncidentIntakeService intakeService,
            IWhatsAppService whatsAppService,
            IConfiguration configuration,
            IDemoAuthService demoAuthService)
        {
            _dataService = dataService;
            _geminiService = geminiService;
            _classificationService = classificationService;
            _weatherService = weatherService;
            _intakeService = intakeService;
            _whatsAppService = whatsAppService;
            _configuration = configuration;
            _demoAuthService = demoAuthService;
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpGet]
        public IActionResult Report()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Report(ReportViewModel model)
        {
            if (!model.ConsentAccepted)
            {
                ModelState.AddModelError("ConsentAccepted", "Please confirm consent and acknowledge CivicOps is not an emergency services replacement.");
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                var result = await _intakeService.ProcessAsync(new IncidentIntakeRequest
                {
                    SourceChannel = SourceChannel.Web,
                    Description = model.Description,
                    Category = model.Category,
                    Suburb = model.Suburb,
                    Ward = model.Ward,
                    ContactName = model.ContactName,
                    ContactPhone = model.ContactPhone,
                    ContactEmail = model.ContactEmail,
                    LocationNotes = string.Join(" | ", new[] { model.LocationNotes, model.CityMunicipality, model.Urgency }.Where(v => !string.IsNullOrWhiteSpace(v))),
                    CreatedBy = "Web/PWA Intake"
                });

                return RedirectToAction("Confirmation", new { reference = result.Incident.ReferenceNumber });
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Error submitting report: {ex.Message}");
                return View(model);
            }
        }

        public async Task<IActionResult> Confirmation(string reference)
        {
            var incident = await _dataService.GetIncidentByReferenceAsync(reference);
            if (incident == null)
            {
                ViewBag.Reference = reference;
                return View("Status", null);
            }

            return View(incident);
        }

        [HttpGet]
        public async Task<IActionResult> Status(string? reference)
        {
            if (string.IsNullOrWhiteSpace(reference))
            {
                ViewBag.Reference = reference;
                return View(null);
            }

            var incident = await _dataService.GetIncidentByReferenceAsync(reference.Trim());
            if (incident == null)
            {
                ViewBag.Reference = reference.Trim();
                return View(null);
            }

            ViewBag.CanSeeAudit = await CurrentStaffCanSeeAsync(incident);
            return View(incident);
        }

        [HttpGet]
        public IActionResult Lookup()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Lookup(string referenceNumber)
        {
            if (string.IsNullOrWhiteSpace(referenceNumber))
            {
                ModelState.AddModelError("", "Please enter a reference number");
                return View();
            }

            var incident = await _dataService.GetIncidentByReferenceAsync(referenceNumber.Trim());
            if (incident == null)
            {
                ModelState.AddModelError("", "Report not found. Please check your reference number.");
                return View();
            }

            return View("Status", incident);
        }

        public async Task<IActionResult> Alerts(string? suburb = null, string? ward = null)
        {
            var alerts = await _dataService.GetAlertsByAreaAsync(suburb, ward);
            ViewBag.Suburb = suburb;
            ViewBag.Ward = ward;
            return View(alerts);
        }

        public async Task<IActionResult> Dashboard()
        {
            var incidents = await _dataService.GetAllIncidentsAsync();
            var alerts = await _dataService.GetAllAlertsAsync();
            var session = await GetCurrentStaffSessionAsync();
            if (session?.Role == UserRole.DepartmentResponder && session.AssignedDepartment.HasValue)
            {
                incidents = incidents.Where(i => i.AssignedDepartment == session.AssignedDepartment.Value).ToList();
                ViewBag.QueueScope = $"Signed in as {session.AssignedDepartment.Value.GetDisplayName()} Department — Showing {session.AssignedDepartment.Value.GetDisplayName()} queue only";
            }
            else if (session?.Role == UserRole.Admin)
            {
                ViewBag.QueueScope = "Signed in as Admin — showing all incidents";
            }
            else if (session?.Role == UserRole.Dispatcher)
            {
                ViewBag.QueueScope = "Signed in as Dispatcher — showing all routing queues";
            }

            var model = new DashboardViewModel
            {
                TotalIncidents = incidents.Count,
                NewIncidents = incidents.Count(i => i.Status == IncidentStatus.New),
                InProgressIncidents = incidents.Count(i => i.Status == IncidentStatus.InProgress),
                EscalatedIncidents = incidents.Count(i => i.Status == IncidentStatus.Escalated),
                ResolvedIncidents = incidents.Count(i => i.Status == IncidentStatus.Resolved || i.Status == IncidentStatus.Closed),
                RecentIncidents = incidents.Take(10).ToList(),
                HighPriorityIncidents = incidents.Where(i => i.Priority == IncidentPriority.Urgent || i.Priority == IncidentPriority.High).Take(5).ToList(),
                ActiveAlerts = alerts.Count,
                IncidentsByDepartment = incidents.GroupBy(i => i.AssignedDepartment)
                    .Select(g => new DepartmentStats { Department = g.Key, Count = g.Count() })
                    .OrderByDescending(d => d.Count)
                    .ToList(),
                IncidentsBySource = incidents.GroupBy(i => i.SourceChannel)
                    .Select(g => new SourceStats { Source = g.Key, Count = g.Count() })
                    .ToList(),
                GeminiStatus = _geminiService.Status
            };

            return View(model);
        }

        public async Task<IActionResult> Department(string dept)
        {
            if (!Enum.TryParse<Department>(dept, true, out var department))
            {
                return NotFound();
            }

            var session = await GetCurrentStaffSessionAsync();
            if (session?.Role == UserRole.DepartmentResponder && session.AssignedDepartment.HasValue && session.AssignedDepartment.Value != department)
            {
                return RedirectToAction("AccessDenied", "Auth");
            }

            var incidents = await _dataService.GetIncidentsByDepartmentAsync(department);
            ViewBag.QueueScope = session?.Role == UserRole.DepartmentResponder ? $"Signed in as {department.GetDisplayName()} Department — Showing {department.GetDisplayName()} queue only" : null;
            ViewBag.DepartmentName = department.GetDisplayName();
            ViewBag.Department = department;
            return View(incidents);
        }

        public async Task<IActionResult> Incident(string id)
        {
            var incident = await _dataService.GetIncidentByIdAsync(id);
            if (incident == null)
            {
                return NotFound();
            }

            if (!await CurrentStaffCanSeeAsync(incident))
            {
                return RedirectToAction("AccessDenied", "Auth");
            }

            return View(incident);
        }

        private async Task<DemoAuthSession?> GetCurrentStaffSessionAsync()
        {
            var sessionId = HttpContext.Session.GetString("SessionId");
            return string.IsNullOrWhiteSpace(sessionId) ? null : await _demoAuthService.GetSessionAsync(sessionId);
        }

        private async Task<bool> CurrentStaffCanSeeAsync(Incident incident)
        {
            var session = await GetCurrentStaffSessionAsync();
            if (session == null) return false;
            if (session.Role == UserRole.Admin || session.Role == UserRole.Dispatcher) return true;
            return session.Role == UserRole.DepartmentResponder && session.AssignedDepartment == incident.AssignedDepartment;
        }

        [HttpPost]
        [DemoAuthorize(UserRole.Dispatcher)]
        public async Task<IActionResult> UpdateStatus(string id, string status, string? publicNote)
        {
            var incident = await _dataService.GetIncidentByIdAsync(id);
            if (incident == null)
            {
                return NotFound();
            }

            if (Enum.TryParse<IncidentStatus>(status, out var newStatus))
            {
                incident.Status = newStatus;
                incident.InternalNotes.Add(new IncidentNote
                {
                    Content = $"Status changed to {newStatus}",
                    Author = "Admin",
                    IsPublic = false
                });

                if (!string.IsNullOrEmpty(publicNote))
                {
                    incident.PublicUpdates.Add(new PublicUpdate 
                    { 
                        Content = publicNote,
                        UpdatedBy = "Admin",
                        RelatedStatus = newStatus
                    });
                }

                await _dataService.UpdateIncidentAsync(incident);
            }

            return RedirectToAction("Incident", new { id });
        }

        public IActionResult Connectors()
        {
            var connectors = new[]
            {
                new ConnectorInfo
                {
                    Name = "Gemini AI Agent",
                    Status = _geminiService.Status,
                    Mode = _geminiService.IsEnabled ? "Live Connector Ready" : "Fallback Active",
                    Description = "AI-powered incident classification and routing",
                    EnvVars = "GEMINI_ENABLED, GEMINI_API_KEY, GEMINI_MODEL, GEMINI_ROUTINE_MODEL, GEMINI_FALLBACK_MODELS, GEMINI_AUTO_RUN_AGENT_PAGE, GEMINI_MANUAL_TEST_COOLDOWN_SECONDS, GEMINI_QUOTA_COOLDOWN_MINUTES, GEMINI_MODE",
                    Documentation = "/Home/BobEvidence and docs/gemini-setup.md"
                },
                new ConnectorInfo
                {
                    Name = "Citizen App / PWA / App Channel",
                    Status = "Installable PWA Ready",
                    Mode = "Backend Gemini/fallback enrichment",
                    Description = "Main public channel for reports, tracking, alerts and profile without WhatsApp dependency",
                    EnvVars = "None on device; Gemini runs on backend only",
                    Documentation = "docs/mobile-pwa.md"
                },
                new ConnectorInfo
                {
                    Name = "Department/ERP Connector Readiness",
                    Status = "Future Connector",
                    Mode = "Pilot-ready architecture",
                    Description = "Department queues can be mapped to municipal ticketing/ERP systems after approvals",
                    EnvVars = "ERP_API_URL, ERP_API_KEY",
                    Documentation = "docs/integration-readiness.md"
                },
                new ConnectorInfo
                {
                    Name = "GIS/Geocoding Connector Readiness",
                    Status = "Future Connector",
                    Mode = "Synthetic ward estimates now",
                    Description = "Real GIS/ward data required for production-grade geocoding",
                    EnvVars = "GIS_API_KEY",
                    Documentation = "docs/integration-readiness.md"
                },
                new ConnectorInfo
                {
                    Name = "Weather/Area Context Connector Readiness",
                    Status = "Sandbox Context",
                    Mode = "Area risk cards",
                    Description = "Weather and area context support alert recommendations",
                    EnvVars = "WEATHER_API_KEY",
                    Documentation = "docs/integration-readiness.md"
                },
                new ConnectorInfo
                {
                    Name = "Email/SMS Connector Readiness",
                    Status = "Future Connector",
                    Mode = "Placeholder",
                    Description = "Approved citizen messaging channels can be added without changing the intake story",
                    EnvVars = "SMS_API_KEY, SMTP_HOST",
                    Documentation = "docs/integration-readiness.md"
                },
                new ConnectorInfo
                {
                    Name = "WhatsApp Optional Connector Readiness",
                    Status = _whatsAppService.GetStatus().Status,
                    Mode = _whatsAppService.GetStatus().Mode,
                    Description = "Optional connector-ready WhatsApp Cloud API integration for future pilots/live-test messaging.",
                    EnvVars = "WHATSAPP_ENABLED, WHATSAPP_DEMO_MODE, WHATSAPP_VERIFY_TOKEN, WHATSAPP_ACCESS_TOKEN, WHATSAPP_PHONE_NUMBER_ID, WHATSAPP_GRAPH_VERSION, WHATSAPP_PUBLIC_BASE_URL",
                    Documentation = "/Home/BobEvidence and docs/whatsapp-setup.md"
                },
                new ConnectorInfo
                {
                    Name = "Voice-note Transcript Readiness",
                    Status = "Future Connector",
                    Mode = "Transcript sandbox",
                    Description = "Voice-note transcripts can enter the same Gemini/fallback intake pipeline",
                    EnvVars = "VOICE_API_KEY, VOICE_SERVICE_URL",
                    Documentation = "docs/integration-readiness.md"
                }
            };

            ViewBag.GeminiStatus = _geminiService.Status;
            ViewBag.GeminiEnabled = _geminiService.IsEnabled;
            ViewBag.GeminiModel = _geminiService.Model;
            ViewBag.GeminiMode = _geminiService.Mode;
            ViewBag.GeminiDiagnostics = _geminiService.GetDiagnostics();
            ViewBag.GeminiApiKeyPresent = !string.IsNullOrWhiteSpace(_configuration["GEMINI_API_KEY"]);
            var whatsApp = _whatsAppService.GetStatus();
            ViewBag.WhatsApp = whatsApp;
            ViewBag.WhatsAppVerifyTokenPresent = !string.IsNullOrWhiteSpace(_configuration["WHATSAPP_VERIFY_TOKEN"]);
            ViewBag.WhatsAppAccessTokenPresent = !string.IsNullOrWhiteSpace(_configuration["WHATSAPP_ACCESS_TOKEN"]);
            ViewBag.WhatsAppPhoneNumberIdPresent = !string.IsNullOrWhiteSpace(_configuration["WHATSAPP_PHONE_NUMBER_ID"]);
            return View(connectors);
        }

        public IActionResult Mobile()
        {
            ViewBag.PwaReady = true;
            ViewBag.ApkExists = System.IO.File.Exists(System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "wwwroot", "downloads", "CivicOpsCitizenCompanion-debug.apk"));
            return View();
        }

        [HttpGet("/app")]
        public IActionResult App()
        {
            ViewBag.PwaReady = true;
            ViewBag.ApkExists = System.IO.File.Exists(System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "wwwroot", "downloads", "CivicOpsCitizenCompanion-debug.apk"));
            ViewBag.AppShell = true;
            return View("Mobile");
        }

        [HttpGet("/app/incident/{reference}")]
        public async Task<IActionResult> AppIncident(string reference)
        {
            var incident = await _dataService.GetIncidentByReferenceAsync(reference.Trim());
            ViewBag.CanSeeAudit = false;
            ViewBag.Reference = reference;
            return View("Status", incident);
        }

        [HttpGet("/app/area/{area}/thread")]
        public async Task<IActionResult> AppAreaThread(string area)
        {
            var normalizedArea = Uri.UnescapeDataString(area).Trim();
            var incidents = (await _dataService.GetAllIncidentsAsync())
                .Where(i => i.NormalizedArea.Equals(normalizedArea, StringComparison.OrdinalIgnoreCase) || i.Suburb.Equals(normalizedArea, StringComparison.OrdinalIgnoreCase))
                .Take(12)
                .ToList();
            ViewBag.Area = normalizedArea;
            return View("AreaThread", incidents);
        }

        public IActionResult DemoTour()
        {
            return View();
        }

        public IActionResult BobEvidence()
        {
            return View();
        }

        public IActionResult Agent()
        {
            ViewBag.GeminiStatus = _geminiService.Status;
            ViewBag.GeminiMode = _geminiService.IsEnabled ? "Gemini" : "Fallback";
            ViewBag.GeminiDiagnostics = _geminiService.GetDiagnostics();
            ViewBag.WhatsAppStatus = _whatsAppService.GetStatus().Status;
            ViewBag.WhatsAppMode = _whatsAppService.GetStatus().Mode;
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> Weather()
        {
            var weatherData = await _weatherService.GetWeatherForAllAreasAsync();
            return View(weatherData);
        }


        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }

    // View Models
    public class ReportViewModel
    {
        public string Description { get; set; } = string.Empty;
        public string? Category { get; set; }
        public string? Suburb { get; set; }
        public string? Ward { get; set; }
        public string? ContactName { get; set; }
        public string? ContactPhone { get; set; }
        public string? ContactEmail { get; set; }
        public string? LocationNotes { get; set; }
        public string? CityMunicipality { get; set; }
        public string? Urgency { get; set; }
        public bool ConsentAccepted { get; set; }
    }

    public class DashboardViewModel
    {
        public int TotalIncidents { get; set; }
        public int NewIncidents { get; set; }
        public int InProgressIncidents { get; set; }
        public int EscalatedIncidents { get; set; }
        public int ResolvedIncidents { get; set; }
        public int ActiveAlerts { get; set; }
        public List<Incident> RecentIncidents { get; set; } = new();
        public List<Incident> HighPriorityIncidents { get; set; } = new();
        public List<DepartmentStats> IncidentsByDepartment { get; set; } = new();
        public List<SourceStats> IncidentsBySource { get; set; } = new();
        public string GeminiStatus { get; set; } = string.Empty;
    }

    public class DepartmentStats
    {
        public Department Department { get; set; }
        public int Count { get; set; }
    }

    public class SourceStats
    {
        public SourceChannel Source { get; set; }
        public int Count { get; set; }
    }

    public class ConnectorInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Mode { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string EnvVars { get; set; } = string.Empty;
        public string Documentation { get; set; } = string.Empty;
    }

    public class ErrorViewModel
    {
        public string? RequestId { get; set; }
        public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
    }
}