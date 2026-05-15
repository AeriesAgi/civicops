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

        public HomeController(
            IDataService dataService,
            IGeminiService geminiService,
            IClassificationService classificationService)
        {
            _dataService = dataService;
            _geminiService = geminiService;
            _classificationService = classificationService;
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
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                // Classify the incident
                ClassificationResult classification;
                if (_geminiService.IsEnabled)
                {
                    classification = await _geminiService.ClassifyWithGeminiAsync(
                        model.Description, 
                        model.Category);
                }
                else
                {
                    classification = await _classificationService.ClassifyIncidentAsync(
                        model.Description, 
                        model.Category);
                }

                // Create incident
                var incident = new Incident
                {
                    SourceChannel = SourceChannel.Web,
                    Description = model.Description,
                    AISummary = classification.Summary,
                    Category = classification.Category,
                    AssignedDepartment = classification.Department,
                    Suburb = model.Suburb ?? "Unknown",
                    Ward = model.Ward ?? "Unknown",
                    Priority = classification.Priority,
                    ContactName = model.ContactName,
                    ContactPhone = model.ContactPhone,
                    ContactEmail = model.ContactEmail,
                    LocationNotes = model.LocationNotes,
                    IsGeminiProcessed = classification.IsGeminiProcessed,
                    ClassificationMethod = classification.Method
                };

                incident.InternalNotes.Add(new IncidentNote
                {
                    Content = $"Incident created via Web. Classified as {classification.Category} using {classification.Method}.",
                    IsPublic = false
                });

                incident.PublicUpdates.Add(new PublicUpdate 
                { 
                    Content = "Your report has been received and assigned to the appropriate department.",
                    UpdatedBy = "System"
                });

                await _dataService.SaveIncidentAsync(incident);

                return RedirectToAction("Confirmation", new { reference = incident.ReferenceNumber });
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
                return NotFound();
            }

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

        [DemoAuthorize]
        public async Task<IActionResult> Dashboard()
        {
            var incidents = await _dataService.GetAllIncidentsAsync();
            var alerts = await _dataService.GetAllAlertsAsync();

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

        [DemoAuthorize]
        public async Task<IActionResult> Department(string dept)
        {
            if (!Enum.TryParse<Department>(dept, true, out var department))
            {
                return NotFound();
            }

            var incidents = await _dataService.GetIncidentsByDepartmentAsync(department);
            ViewBag.DepartmentName = department.GetDisplayName();
            ViewBag.Department = department;
            return View(incidents);
        }

        [DemoAuthorize]
        public async Task<IActionResult> Incident(string id)
        {
            var incident = await _dataService.GetIncidentByIdAsync(id);
            if (incident == null)
            {
                return NotFound();
            }

            return View(incident);
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

        [DemoAuthorize]
        public IActionResult Connectors()
        {
            var connectors = new[]
            {
                new ConnectorInfo
                {
                    Name = "Gemini AI",
                    Status = _geminiService.Status,
                    Mode = _geminiService.IsEnabled ? "Configured" : "Demo",
                    Description = "AI-powered incident classification and routing",
                    EnvVars = "GEMINI_API_KEY, GEMINI_MODEL, GEMINI_ENABLED",
                    Documentation = "/docs/gemini-setup.md"
                },
                new ConnectorInfo
                {
                    Name = "WhatsApp Cloud API",
                    Status = "Demo Mode",
                    Mode = "Demo",
                    Description = "WhatsApp message intake (requires Meta app setup)",
                    EnvVars = "WHATSAPP_VERIFY_TOKEN, WHATSAPP_ACCESS_TOKEN, WHATSAPP_PHONE_NUMBER_ID",
                    Documentation = "/docs/whatsapp-setup.md"
                },
                new ConnectorInfo
                {
                    Name = "Voice Transcription",
                    Status = "Future Connector",
                    Mode = "Placeholder",
                    Description = "Audio-to-text transcription service",
                    EnvVars = "VOICE_API_KEY, VOICE_SERVICE_URL",
                    Documentation = "Future integration"
                },
                new ConnectorInfo
                {
                    Name = "SMS Notifications",
                    Status = "Future Connector",
                    Mode = "Placeholder",
                    Description = "SMS notification service",
                    EnvVars = "SMS_API_KEY, SMS_FROM_NUMBER",
                    Documentation = "Future integration"
                },
                new ConnectorInfo
                {
                    Name = "Email Notifications",
                    Status = "Future Connector",
                    Mode = "Placeholder",
                    Description = "Email notification service",
                    EnvVars = "SMTP_HOST, SMTP_PORT, SMTP_USER, SMTP_PASSWORD",
                    Documentation = "Future integration"
                },
                new ConnectorInfo
                {
                    Name = "GIS/Geocoding",
                    Status = "Future Connector",
                    Mode = "Placeholder",
                    Description = "Location mapping and geocoding",
                    EnvVars = "GIS_API_KEY",
                    Documentation = "Future integration"
                },
                new ConnectorInfo
                {
                    Name = "Municipal ERP",
                    Status = "Future Connector",
                    Mode = "Placeholder",
                    Description = "Integration with municipal ticketing systems",
                    EnvVars = "ERP_API_URL, ERP_API_KEY",
                    Documentation = "Future integration"
                }
            };

            return View(connectors);
        }

        public IActionResult Mobile()
        {
            return View();
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