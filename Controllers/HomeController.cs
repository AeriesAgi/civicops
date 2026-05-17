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
        private readonly IResidentAuthService _residentAuthService;

        public HomeController(
            IDataService dataService,
            IGeminiService geminiService,
            IClassificationService classificationService,
            IWeatherService weatherService,
            IIncidentIntakeService intakeService,
            IWhatsAppService whatsAppService,
            IConfiguration configuration,
            IDemoAuthService demoAuthService,
            IResidentAuthService residentAuthService)
        {
            _dataService = dataService;
            _geminiService = geminiService;
            _classificationService = classificationService;
            _weatherService = weatherService;
            _intakeService = intakeService;
            _whatsAppService = whatsAppService;
            _configuration = configuration;
            _demoAuthService = demoAuthService;
            _residentAuthService = residentAuthService;
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

        [HttpGet("/Home/Incident/{id?}")]
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
        [DemoAuthorize]
        public async Task<IActionResult> UpdateStatus(string id, string status, string? priority, string? actionType, string? publicNote, string? internalNote)
        {
            var incident = await _dataService.GetIncidentByIdAsync(id);
            if (incident == null) return NotFound();
            if (!await CurrentStaffCanSeeAsync(incident)) return RedirectToAction("AccessDenied", "Auth");

            var session = await GetCurrentStaffSessionAsync();
            var actor = session?.Email ?? "CivicOps Staff";
            var previousStatus = incident.Status;

            var targetStatus = status;
            if (!string.IsNullOrWhiteSpace(actionType))
            {
                targetStatus = actionType switch
                {
                    "acknowledge" => nameof(IncidentStatus.Acknowledged),
                    "in-progress" => nameof(IncidentStatus.InProgress),
                    "request-info" => nameof(IncidentStatus.WaitingForCitizen),
                    "escalate" => nameof(IncidentStatus.Escalated),
                    "recommend-alert" => nameof(IncidentStatus.AlertRecommended),
                    "resolve" => nameof(IncidentStatus.Resolved),
                    "close" => nameof(IncidentStatus.Closed),
                    _ => status
                };
            }

            if (Enum.TryParse<IncidentStatus>(targetStatus, true, out var newStatus))
            {
                incident.Status = newStatus;
                incident.StatusHistory.Add(new IncidentStatusHistory
                {
                    FromStatus = previousStatus,
                    ToStatus = newStatus,
                    ChangedBy = actor,
                    Reason = string.IsNullOrWhiteSpace(internalNote) ? $"Action: {actionType ?? "status update"}" : internalNote
                });
            }

            if (Enum.TryParse<IncidentPriority>(priority, true, out var newPriority)) incident.Priority = newPriority;
            if (actionType == "escalate") incident.Priority = IncidentPriority.Critical;
            if (actionType == "recommend-alert") incident.AlertRecommendation = $"Area alert recommended by {actor}: {publicNote ?? "Review duplicate reports and notify affected residents."}";
            if (actionType == "generate-brief") incident.DepartmentBrief = $"Department brief generated for {incident.AssignedDepartment.GetDisplayName()}: {incident.Category} in {incident.NormalizedArea}. Priority {incident.Priority}; {incident.AffectedCount} affected confirmations. Next step: acknowledge, assign crew/resources, and post public update.";
            if (actionType == "generate-citizen-response") incident.CitizenResponse = $"Reference {incident.ReferenceNumber}: your report is {FormatStatus(incident.Status)} with {incident.AssignedDepartment.GetDisplayName()}. We will post public updates as the department acts.";

            var defaultPublicUpdate = BuildDefaultPublicUpdate(actionType, incident);
            var defaultInternalNote = BuildDefaultInternalNote(actionType, incident);
            if (string.IsNullOrWhiteSpace(publicNote)) publicNote = defaultPublicUpdate;
            if (string.IsNullOrWhiteSpace(internalNote)) internalNote = defaultInternalNote;

            if (!string.IsNullOrWhiteSpace(internalNote) || !string.IsNullOrWhiteSpace(actionType))
            {
                incident.InternalNotes.Add(new IncidentNote
                {
                    Content = string.IsNullOrWhiteSpace(internalNote) ? $"Workflow action completed: {actionType}." : internalNote,
                    Author = actor,
                    IsPublic = false
                });
            }

            if (!string.IsNullOrWhiteSpace(publicNote))
            {
                incident.PublicUpdates.Add(new PublicUpdate
                {
                    Content = publicNote,
                    UpdatedBy = actor,
                    RelatedStatus = incident.Status
                });
            }

            await _dataService.UpdateIncidentAsync(incident);
            TempData["IncidentActionMessage"] = $"{FormatStatus(incident.Status)} action saved for {incident.ReferenceNumber}. Timeline, public updates and audit notes are current.";
            return RedirectToAction("Incident", new { id });
        }

        private static string BuildDefaultPublicUpdate(string? actionType, Incident incident) => actionType switch
        {
            "acknowledge" => $"{incident.AssignedDepartment.GetDisplayName()} has acknowledged report {incident.ReferenceNumber} and is reviewing the next operational step.",
            "in-progress" => $"Work is now in progress for {incident.ReferenceNumber}. The assigned department will post updates as field information is confirmed.",
            "request-info" => $"More information is required for {incident.ReferenceNumber}. Please add location details or respond through the Citizen App if requested.",
            "escalate" => $"Report {incident.ReferenceNumber} has been escalated for urgent municipal attention.",
            "recommend-alert" => $"An area alert has been recommended for {incident.NormalizedArea}. It will be published after human review.",
            "resolve" => $"Report {incident.ReferenceNumber} has been marked resolved. Thank you for helping improve civic visibility.",
            "close" => $"Report {incident.ReferenceNumber} has been closed after municipal review.",
            "generate-citizen-response" => incident.CitizenResponse,
            _ => null
        };

        private static string BuildDefaultInternalNote(string? actionType, Incident incident) => actionType switch
        {
            "generate-brief" => $"Generated department brief for {incident.AssignedDepartment.GetDisplayName()} using current incident context and affected confirmations.",
            "generate-citizen-response" => "Drafted citizen response from current status, department and public-update context.",
            "recommend-alert" => $"Recommended area alert review for {incident.NormalizedArea}; dispatcher approval still required before publication.",
            _ => string.IsNullOrWhiteSpace(actionType) ? string.Empty : $"Workflow action completed: {actionType}."
        };

        private static string FormatStatus(IncidentStatus status) => status switch
        {
            IncidentStatus.InProgress => "In Progress",
            IncidentStatus.WaitingForCitizen => "Waiting for Citizen",
            IncidentStatus.AlertRecommended => "Alert Recommended",
            _ => status.ToString()
        };

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
                    EnvVars = "Gemini__Enabled, Gemini__Mode, Gemini__Model, Gemini__PremiumModel, Gemini__RoutineModel, Gemini__FallbackModels, GEMINI_API_KEY (legacy env aliases GEMINI_ENABLED, GEMINI_MODEL, GEMINI_ROUTINE_MODEL supported)",
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

        [HttpGet("/citizen-app")]
        [HttpGet("/download-app")]
        [HttpGet("/install-app")]
        public IActionResult CitizenApp()
        {
            ViewBag.PwaReady = true;
            ViewBag.ApkExists = System.IO.File.Exists(System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "wwwroot", "downloads", "CivicOpsCitizenCompanion-debug.apk"));
            return View("Mobile");
        }

        public IActionResult Mobile()
        {
            return RedirectToAction(nameof(CitizenApp));
        }

        [HttpGet("/app")]
        public IActionResult App()
        {
            PrepareAppShell();
            return View("Mobile");
        }

        [HttpGet("/app/login")]
        public IActionResult AppLogin()
        {
            PrepareAppShell(forceLogin: true);
            return View("Mobile");
        }

        [HttpGet("/app/signup")]
        public IActionResult AppSignup()
        {
            PrepareAppShell(forceLogin: true, signupMode: true);
            return View("Mobile");
        }


        [HttpGet("/app/report")]
        public IActionResult AppReport()
        {
            PrepareAppShell(startSection: "report");
            return View("Mobile");
        }

        [HttpGet("/app/tickets")]
        public IActionResult AppTickets()
        {
            PrepareAppShell(startSection: "tickets");
            return View("Mobile");
        }

        [HttpGet("/app/alerts")]
        public IActionResult AppAlerts()
        {
            PrepareAppShell(startSection: "alerts");
            return View("Mobile");
        }

        [HttpGet("/app/profile")]
        public IActionResult AppProfile()
        {
            PrepareAppShell(startSection: "profile");
            return View("Mobile");
        }

        [HttpGet("/app/copilot")]
        public IActionResult AppCopilot()
        {
            PrepareAppShell(startSection: "copilot");
            return View("Mobile");
        }

        [HttpPost("/app/demo-resident")]
        public async Task<IActionResult> AppDemoResident()
        {
            var user = await _residentAuthService.AuthenticateAsync("resident@civicops.demo", "CivicOps2026!");
            if (user != null)
            {
                HttpContext.Session.SetString("ResidentUserId", user.Id);
                HttpContext.Session.SetString("ResidentUserEmail", user.Email);
                HttpContext.Session.SetString("ResidentUserName", user.FullName);
            }
            HttpContext.Session.SetString("AppDemoAccess", "true");
            TempData["AppMessage"] = "Demo resident access enabled for the Citizen App.";
            return Redirect("/app");
        }

        [HttpPost("/app/create-demo-profile")]
        public async Task<IActionResult> AppCreateDemoProfile(string? displayName, string? area)
        {
            var safeName = string.IsNullOrWhiteSpace(displayName) ? "Demo Resident" : displayName.Trim();
            var email = $"appdemo-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}-{Guid.NewGuid():N}@civicops.demo";
            var user = await _residentAuthService.CreateUserAsync(email, "CivicOps2026!", safeName);
            user.AreaSuburb = string.IsNullOrWhiteSpace(area) ? "Chatsworth" : area.Trim();
            user.FollowedSuburbs.Add(user.AreaSuburb);
            await _residentAuthService.UpdateUserAsync(user);
            HttpContext.Session.SetString("ResidentUserId", user.Id);
            HttpContext.Session.SetString("ResidentUserEmail", user.Email);
            HttpContext.Session.SetString("ResidentUserName", user.FullName);
            HttpContext.Session.SetString("AppDemoAccess", "true");
            TempData["AppMessage"] = "Demo profile created. You can now use the Citizen App dashboard.";
            return Redirect("/app");
        }

        [HttpPost("/app/logout")]
        public IActionResult AppLogout()
        {
            HttpContext.Session.Remove("ResidentUserId");
            HttpContext.Session.Remove("ResidentUserEmail");
            HttpContext.Session.Remove("ResidentUserName");
            HttpContext.Session.Remove("AppDemoAccess");
            return Redirect("/app/login");
        }

        private void PrepareAppShell(bool forceLogin = false, bool signupMode = false, string? startSection = null)
        {
            ViewBag.PwaReady = true;
            ViewBag.ApkExists = System.IO.File.Exists(System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "wwwroot", "downloads", "CivicOpsCitizenCompanion-debug.apk"));
            ViewBag.AppShell = true;
            ViewBag.AppAuthenticated = !forceLogin && (HttpContext.Session.GetString("ResidentUserId") != null || HttpContext.Session.GetString("AppDemoAccess") == "true");
            ViewBag.AppSignupMode = signupMode;
            ViewBag.AppStartSection = startSection;
            ViewBag.AppResidentName = HttpContext.Session.GetString("ResidentUserName") ?? "Demo Resident";
            ViewBag.AppMessage = TempData["AppMessage"] as string;
        }

        [HttpGet("/app/incident/{reference}")]
        public async Task<IActionResult> AppIncident(string reference)
        {
            PrepareAppShell(startSection: "tickets");
            var incident = await _dataService.GetIncidentByReferenceAsync(reference.Trim());
            ViewBag.CanSeeAudit = false;
            ViewBag.Reference = reference;
            ViewBag.AppTrackedIncident = incident;
            return View("Mobile");
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