using CivicOps.Models;
using CivicOps.Services;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CivicOps.Controllers
{
    [ApiController]
    [Route("api")]
    public class ApiController : ControllerBase
    {
        private readonly IDataService _dataService;
        private readonly IGeminiService _geminiService;
        private readonly IClassificationService _classificationService;
        private readonly IIncidentIntakeService _intakeService;
        private readonly IWhatsAppService _whatsAppService;
        private readonly IConfiguration _configuration;

        public ApiController(
            IDataService dataService,
            IGeminiService geminiService,
            IClassificationService classificationService,
            IIncidentIntakeService intakeService,
            IWhatsAppService whatsAppService,
            IConfiguration configuration)
        {
            _dataService = dataService;
            _geminiService = geminiService;
            _classificationService = classificationService;
            _intakeService = intakeService;
            _whatsAppService = whatsAppService;
            _configuration = configuration;
        }

        [HttpPost("reports")]
        public async Task<IActionResult> SubmitReport([FromBody] ReportSubmission submission)
        {
            try
            {
                var result = await _intakeService.ProcessAsync(new IncidentIntakeRequest
                {
                    SourceChannel = submission.SourceChannel,
                    Description = submission.Description,
                    Category = submission.Category,
                    Suburb = submission.Suburb,
                    Ward = submission.Ward,
                    ContactName = submission.ContactName,
                    ContactPhone = submission.ContactPhone,
                    ContactEmail = submission.ContactEmail,
                    LocationNotes = submission.LocationNotes,
                    MediaMetadata = submission.MediaMetadata,
                    AudioMetadata = submission.AudioMetadata,
                    CreatedBy = "Mobile/API Intake"
                });

                var incident = result.Incident;
                return Ok(new
                {
                    success = true,
                    referenceNumber = incident.ReferenceNumber,
                    validation = result.Validation,
                    department = incident.AssignedDepartment.GetDisplayName(),
                    status = incident.Status.ToString(),
                    priority = incident.Priority.ToString(),
                    citizenResponse = result.CitizenResponse,
                    alertRecommendation = result.AlertRecommendation,
                    message = "Report submitted successfully"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpGet("reports/{reference}")]
        public async Task<IActionResult> GetReportByReference(string reference)
        {
            var incident = await _dataService.GetIncidentByReferenceAsync(reference);
            if (incident == null)
            {
                return NotFound(new { success = false, message = "Report not found" });
            }

            return Ok(new
            {
                success = true,
                referenceNumber = incident.ReferenceNumber,
                status = incident.Status.ToString(),
                department = incident.AssignedDepartment.GetDisplayName(),
                category = incident.Category,
                suburb = incident.Suburb,
                ward = incident.Ward,
                priority = incident.Priority.ToString(),
                createdAt = incident.CreatedAt,
                lastUpdatedAt = incident.LastUpdatedAt,
                latestUpdate = incident.PublicUpdates.LastOrDefault(),
                publicUpdates = incident.PublicUpdates
            });
        }

        [HttpGet("alerts")]
        public async Task<IActionResult> GetAlerts([FromQuery] string? area = null, [FromQuery] string? ward = null)
        {
            var alerts = await _dataService.GetAlertsByAreaAsync(area, ward);
            
            return Ok(new
            {
                success = true,
                count = alerts.Count,
                alerts = alerts.Select(a => new
                {
                    id = a.Id,
                    type = a.Type.ToString(),
                    severity = a.Severity.ToString(),
                    title = a.Title,
                    description = a.Description,
                    suburb = a.Suburb,
                    ward = a.Ward,
                    department = a.AffectedDepartment.GetDisplayName(),
                    createdAt = a.CreatedAt,
                    expiresAt = a.ExpiresAt
                })
            });
        }

        [HttpGet("departments")]
        public IActionResult GetDepartments()
        {
            var departments = Enum.GetValues<Department>()
                .Select(d => new
                {
                    id = d.ToString(),
                    name = d.GetDisplayName()
                });

            return Ok(new { success = true, departments });
        }

        [HttpGet("departments/{department}/queue")]
        public async Task<IActionResult> GetDepartmentQueue(string department)
        {
            if (!Enum.TryParse<Department>(department, true, out var dept))
            {
                return BadRequest(new { success = false, message = "Invalid department" });
            }

            var incidents = await _dataService.GetIncidentsByDepartmentAsync(dept);
            
            return Ok(new
            {
                success = true,
                department = dept.GetDisplayName(),
                count = incidents.Count,
                incidents = incidents.Select(i => new
                {
                    id = i.Id,
                    referenceNumber = i.ReferenceNumber,
                    category = i.Category,
                    status = i.Status.ToString(),
                    priority = i.Priority.ToString(),
                    suburb = i.Suburb,
                    ward = i.Ward,
                    createdAt = i.CreatedAt,
                    summary = i.AISummary
                })
            });
        }

        [HttpGet("incidents/{id}")]
        public async Task<IActionResult> GetIncident(string id)
        {
            var incident = await _dataService.GetIncidentByIdAsync(id);
            if (incident == null)
            {
                return NotFound(new { success = false, message = "Incident not found" });
            }

            return Ok(new
            {
                success = true,
                incident = new
                {
                    id = incident.Id,
                    referenceNumber = incident.ReferenceNumber,
                    sourceChannel = incident.SourceChannel.ToString(),
                    description = incident.Description,
                    summary = incident.AISummary,
                    category = incident.Category,
                    department = incident.AssignedDepartment.GetDisplayName(),
                    suburb = incident.Suburb,
                    ward = incident.Ward,
                    status = incident.Status.ToString(),
                    priority = incident.Priority.ToString(),
                    createdAt = incident.CreatedAt,
                    lastUpdatedAt = incident.LastUpdatedAt,
                    publicUpdates = incident.PublicUpdates,
                    internalNotes = incident.InternalNotes.Select(n => new
                    {
                        timestamp = n.Timestamp,
                        author = n.Author,
                        content = n.Content,
                        isPublic = n.IsPublic
                    }),
                    classificationMethod = incident.ClassificationMethod,
                    isGeminiProcessed = incident.IsGeminiProcessed
                }
            });
        }

        [HttpPost("incidents/{id}/status")]
        public async Task<IActionResult> UpdateStatus(string id, [FromBody] StatusUpdate update)
        {
            var incident = await _dataService.GetIncidentByIdAsync(id);
            if (incident == null)
            {
                return NotFound(new { success = false, message = "Incident not found" });
            }

            if (Enum.TryParse<IncidentStatus>(update.Status, true, out var newStatus))
            {
                incident.Status = newStatus;
                incident.InternalNotes.Add(new IncidentNote
                {
                    Content = $"Status changed to {newStatus}",
                    Author = update.Author ?? "System",
                    IsPublic = false
                });

                if (!string.IsNullOrEmpty(update.PublicNote))
                {
                    incident.PublicUpdates.Add(new PublicUpdate 
                    { 
                        Content = update.PublicNote,
                        UpdatedBy = update.Author ?? "System",
                        RelatedStatus = newStatus
                    });
                }

                await _dataService.UpdateIncidentAsync(incident);

                return Ok(new { success = true, message = "Status updated" });
            }

            return BadRequest(new { success = false, message = "Invalid status" });
        }

        [HttpPost("incidents/{id}/note")]
        public async Task<IActionResult> AddNote(string id, [FromBody] NoteSubmission note)
        {
            var incident = await _dataService.GetIncidentByIdAsync(id);
            if (incident == null)
            {
                return NotFound(new { success = false, message = "Incident not found" });
            }

            var incidentNote = new IncidentNote
            {
                Content = note.Content,
                Author = note.Author ?? "System",
                IsPublic = note.IsPublic
            };

            incident.InternalNotes.Add(incidentNote);

            if (note.IsPublic)
            {
                incident.PublicUpdates.Add(new PublicUpdate 
                { 
                    Content = note.Content,
                    UpdatedBy = note.Author ?? "System"
                });
            }

            await _dataService.UpdateIncidentAsync(incident);

            return Ok(new { success = true, message = "Note added" });
        }

        [HttpPost("incidents/{id}/escalate")]
        public async Task<IActionResult> EscalateIncident(string id, [FromBody] EscalationRequest request)
        {
            var incident = await _dataService.GetIncidentByIdAsync(id);
            if (incident == null)
            {
                return NotFound(new { success = false, message = "Incident not found" });
            }

            incident.Status = IncidentStatus.Escalated;
            incident.Priority = IncidentPriority.Urgent;
            
            incident.InternalNotes.Add(new IncidentNote
            {
                Content = $"Incident escalated. Reason: {request.Reason}",
                Author = request.Author ?? "System",
                IsPublic = false
            });

            incident.PublicUpdates.Add(new PublicUpdate 
            { 
                Content = "Your report has been escalated for urgent attention.",
                UpdatedBy = request.Author ?? "System"
            });

            await _dataService.UpdateIncidentAsync(incident);

            return Ok(new { success = true, message = "Incident escalated" });
        }



        [HttpGet("agent/scenarios")]
        public IActionResult GetAgentScenarios()
        {
            var whatsApp = _whatsAppService.GetStatus();
            return Ok(new
            {
                success = true,
                agentStatus = "Operational",
                intakePipelineStatus = "Unified IncidentIntakeService online",
                gemini = new
                {
                    enabled = _geminiService.IsEnabled,
                    status = _geminiService.IsEnabled ? "Live connector ready" : _geminiService.Status,
                    mode = _geminiService.Mode,
                    model = _geminiService.Model,
                    source = _geminiService.IsEnabled ? "Gemini" : "Deterministic fallback"
                },
                whatsapp = BuildWhatsAppConnectorSnapshot(whatsApp),
                scenarios = new[]
                {
                    new { id = "latest-report", label = "Analyze latest resident report", sourceChannel = "Web" },
                    new { id = "whatsapp-report", label = "Analyze WhatsApp report", sourceChannel = "WhatsApp" },
                    new { id = "voice-note", label = "Analyze voice-note transcript", sourceChannel = "VoiceNote" },
                    new { id = "gemini-health", label = "Run live Gemini health test", sourceChannel = "Connector" },
                    new { id = "citizen-response", label = "Generate citizen response", sourceChannel = "Web" },
                    new { id = "department-brief", label = "Generate department brief", sourceChannel = "Operations" },
                    new { id = "area-alert", label = "Recommend area alert", sourceChannel = "Operations" },
                    new { id = "judge-summary", label = "Generate judge summary", sourceChannel = "Operations" }
                }
            });
        }

        [HttpPost("agent/run")]
        public async Task<IActionResult> RunAgentScenario([FromBody] AgentRunRequest request)
        {
            var scenario = string.IsNullOrWhiteSpace(request?.Scenario) ? "latest-report" : request.Scenario.Trim();

            if (scenario.Equals("gemini-health", StringComparison.OrdinalIgnoreCase))
            {
                var health = await _geminiService.TestConnectionAsync();
                return Ok(new
                {
                    success = true,
                    action = "Gemini health test",
                    sourceChannel = "Connector",
                    geminiAssisted = health.Success,
                    aiSource = health.Success ? "Gemini" : "Deterministic fallback",
                    validation = health.Success ? "Live Gemini connector responded." : "Fallback active — live Gemini activates when GEMINI_API_KEY and GEMINI_ENABLED=true are configured.",
                    department = "CivicOps platform",
                    priority = health.Success ? "Ready" : "Fallback",
                    routingReason = health.Message,
                    citizenResponse = "Connector check completed. Results are shown in the audit trail.",
                    referenceNumber = (string?)null,
                    alertRecommendation = "No public alert generated from connector health check.",
                    auditNotes = new[] { health.Status, $"Model: {health.Model}", $"Mode: {health.Mode}" },
                    whatsapp = BuildWhatsAppConnectorSnapshot(_whatsAppService.GetStatus()),
                    timestamp = DateTime.UtcNow
                });
            }

            var intake = BuildAgentIntakeRequest(scenario, request);
            var result = await _intakeService.ProcessAsync(intake);
            var incident = result.Incident;
            var whatsAppStatus = _whatsAppService.GetStatus();
            var action = ScenarioLabel(scenario);

            return Ok(new
            {
                success = true,
                action,
                sourceChannel = incident.SourceChannel.ToString(),
                geminiAssisted = incident.IsGeminiProcessed,
                aiSource = incident.IsGeminiProcessed ? "Gemini assisted" : "Deterministic fallback",
                validation = result.Validation,
                department = incident.AssignedDepartment.GetDisplayName(),
                priority = incident.Priority.ToString(),
                category = incident.Category,
                summary = incident.AISummary,
                routingReason = result.RoutingReason,
                citizenResponse = result.CitizenResponse,
                referenceNumber = incident.ReferenceNumber,
                incidentUrl = Url.Action("Incident", "Home", new { id = incident.Id }),
                alertRecommendation = ScenarioAlertRecommendation(scenario, result.AlertRecommendation),
                auditNotes = new[]
                {
                    $"Created via shared IncidentIntakeService from {incident.SourceChannel}.",
                    $"Classification method: {incident.ClassificationMethod}.",
                    incident.IsGeminiProcessed ? "Gemini assisted classification was used." : "Fallback active — live Gemini activates when GEMINI_API_KEY and GEMINI_ENABLED=true are configured.",
                    whatsAppStatus.CanSend ? "WhatsApp Cloud API send ready." : "Sandbox WhatsApp flow active — Cloud API activates through env vars.",
                    "Human-in-the-loop review remains required before field dispatch or public alerting."
                },
                whatsapp = BuildWhatsAppConnectorSnapshot(whatsAppStatus),
                timestamp = DateTime.UtcNow
            });
        }

        [HttpPost("agent/generate-response")]
        public Task<IActionResult> GenerateAgentResponse([FromBody] AgentRunRequest request)
        {
            request ??= new AgentRunRequest();
            request.Scenario = "citizen-response";
            return RunAgentScenario(request);
        }

        [HttpPost("agent/recommend-alert")]
        public Task<IActionResult> RecommendAgentAlert([FromBody] AgentRunRequest request)
        {
            request ??= new AgentRunRequest();
            request.Scenario = "area-alert";
            return RunAgentScenario(request);
        }

        [HttpGet("connectors/gemini/test")]
        public async Task<IActionResult> TestGeminiConnector()
        {
            var result = await _geminiService.TestConnectionAsync();
            return Ok(new
            {
                success = result.Success,
                status = result.Status,
                model = result.Model,
                mode = result.Mode,
                message = result.Message
            });
        }

        [HttpGet("connectors/status")]
        public IActionResult GetConnectorStatus()
        {
            var connectors = new[]
            {
                new
                {
                    name = "Gemini AI",
                    status = _geminiService.Status,
                    mode = _geminiService.IsEnabled ? "Live Ready" : "Fallback Active",
                    description = "AI-powered incident classification and routing"
                },
                new
                {
                    name = "WhatsApp Cloud API",
                    status = _whatsAppService.GetStatus().Status,
                    mode = _whatsAppService.GetStatus().Mode,
                    description = "WhatsApp message intake (requires Meta app setup)"
                },
                new
                {
                    name = "Voice Transcription",
                    status = "Future Connector",
                    mode = "Future Connector",
                    description = "Audio-to-text transcription service"
                },
                new
                {
                    name = "SMS Notifications",
                    status = "Future Connector",
                    mode = "Future Connector",
                    description = "SMS notification service"
                },
                new
                {
                    name = "Email Notifications",
                    status = "Future Connector",
                    mode = "Future Connector",
                    description = "Email notification service"
                },
                new
                {
                    name = "GIS/Geocoding",
                    status = "Future Connector",
                    mode = "Future Connector",
                    description = "Location mapping and geocoding"
                },
                new
                {
                    name = "Municipal ERP",
                    status = "Future Connector",
                    mode = "Future Connector",
                    description = "Integration with municipal ticketing systems"
                }
            };

            return Ok(new { success = true, connectors });
        }


        private IncidentIntakeRequest BuildAgentIntakeRequest(string scenario, AgentRunRequest? request)
        {
            var description = !string.IsNullOrWhiteSpace(request?.Input) ? request.Input.Trim() : ScenarioDescription(scenario);
            var source = scenario switch
            {
                "whatsapp-report" => SourceChannel.WhatsApp,
                "voice-note" => SourceChannel.VoiceNote,
                _ => SourceChannel.Web
            };

            return new IncidentIntakeRequest
            {
                SourceChannel = source,
                Description = description,
                Category = request?.Category,
                Suburb = string.IsNullOrWhiteSpace(request?.Suburb) ? ScenarioSuburb(scenario) : request.Suburb,
                Ward = string.IsNullOrWhiteSpace(request?.Ward) ? ScenarioWard(scenario) : request.Ward,
                ContactName = request?.ContactName ?? "Synthetic resident",
                ContactPhone = request?.ContactPhone,
                ContactEmail = request?.ContactEmail,
                LocationNotes = request?.LocationNotes ?? "Synthetic civic operations scenario for judging.",
                MediaMetadata = scenario == "whatsapp-report" ? "optional WhatsApp media metadata: image/audio placeholder" : request?.MediaMetadata,
                AudioMetadata = scenario == "voice-note" ? "voice-note-transcript-sandbox.wav; transcript supplied" : request?.AudioMetadata,
                CreatedBy = "CivicOps AI Agent Command Centre",
                ConnectorMetadata = new Dictionary<string, string>
                {
                    ["agent_scenario"] = scenario,
                    ["data_note"] = "Synthetic operational data; live connector-ready when credentials are configured.",
                    ["human_review"] = "Required before dispatch, public alert, or external escalation."
                }
            };
        }

        private static string ScenarioDescription(string scenario) => scenario switch
        {
            "whatsapp-report" => "WhatsApp resident report: Water has been running down Quarry Road since 05:30, road edge is flooding and school traffic is affected near the bus stop.",
            "voice-note" => "Voice-note transcript: There is a fallen electricity cable sparking near the community hall after the rain. People are walking close to it and the street lights are out.",
            "citizen-response" => "Resident reports repeated refuse collection misses for three weeks on Mkhize Street with bags now blocking the pavement and attracting animals.",
            "department-brief" => "Three related pothole and blocked stormwater reports around Phoenix Highway after heavy rainfall; traffic is slowing and residents request repair scheduling.",
            "area-alert" => "Multiple reports of stormwater drains overflowing in Chatsworth Ward 73 after heavy rain, with water approaching low-lying homes.",
            "judge-summary" => "CivicOps judging flow: messy web, WhatsApp, mobile, and voice-note reports are validated, classified, routed, acknowledged, audited, and shown on dashboards with live connector readiness.",
            _ => "Latest resident report scenario: Burst water pipe on Main Road causing flooding near a taxi stop and affecting morning commuters."
        };

        private static string ScenarioSuburb(string scenario) => scenario switch
        {
            "whatsapp-report" => "Chatsworth",
            "voice-note" => "Umlazi",
            "department-brief" => "Phoenix",
            "area-alert" => "Chatsworth",
            _ => "Durban"
        };

        private static string ScenarioWard(string scenario) => scenario switch
        {
            "whatsapp-report" => "Ward 73",
            "voice-note" => "Ward 80",
            "department-brief" => "Ward 52",
            "area-alert" => "Ward 73",
            _ => "Ward 00"
        };

        private static string ScenarioLabel(string scenario) => scenario switch
        {
            "whatsapp-report" => "Analyze WhatsApp report",
            "voice-note" => "Analyze voice-note transcript",
            "citizen-response" => "Generate citizen response",
            "department-brief" => "Generate department brief",
            "area-alert" => "Recommend area alert",
            "judge-summary" => "Generate judge summary",
            _ => "Analyze latest resident report"
        };

        private static string ScenarioAlertRecommendation(string scenario, string fallback) => scenario == "area-alert"
            ? "Recommend area alert review: cluster stormwater reports by suburb/ward, verify with dispatcher, then publish a human-approved area notice if duplicate reports continue."
            : fallback;

        private object BuildWhatsAppConnectorSnapshot(WhatsAppStatus status) => new
        {
            enabled = status.Enabled,
            demoMode = status.DemoMode,
            canSend = status.CanSend,
            mode = status.Mode,
            status = status.Status,
            graphVersion = status.GraphVersion,
            publicBaseUrlPresent = !string.IsNullOrWhiteSpace(status.PublicBaseUrl),
            publicBaseUrl = status.PublicBaseUrl,
            verifyTokenPresent = !string.IsNullOrWhiteSpace(_configuration["WHATSAPP_VERIFY_TOKEN"]),
            accessTokenPresent = !string.IsNullOrWhiteSpace(_configuration["WHATSAPP_ACCESS_TOKEN"]),
            phoneNumberIdPresent = !string.IsNullOrWhiteSpace(_configuration["WHATSAPP_PHONE_NUMBER_ID"]),
            callbackUrl = string.IsNullOrWhiteSpace(status.PublicBaseUrl) ? null : $"{status.PublicBaseUrl.TrimEnd('/')}/webhooks/whatsapp"
        };

    }

    // Request/Response models
    public class ReportSubmission
    {
        public SourceChannel SourceChannel { get; set; } = SourceChannel.Web;
        public string Description { get; set; } = string.Empty;
        public string? Category { get; set; }
        public string? Suburb { get; set; }
        public string? Ward { get; set; }
        public string? ContactName { get; set; }
        public string? ContactPhone { get; set; }
        public string? ContactEmail { get; set; }
        public string? LocationNotes { get; set; }
        public string? MediaMetadata { get; set; }
        public string? AudioMetadata { get; set; }
    }

    public class StatusUpdate
    {
        public string Status { get; set; } = string.Empty;
        public string? PublicNote { get; set; }
        public string? Author { get; set; }
    }

    public class NoteSubmission
    {
        public string Content { get; set; } = string.Empty;
        public string? Author { get; set; }
        public bool IsPublic { get; set; }
    }

    public class EscalationRequest
    {
        public string Reason { get; set; } = string.Empty;
        public string? Author { get; set; }
    }

    public class AgentRunRequest
    {
        public string Scenario { get; set; } = string.Empty;
        public string? Input { get; set; }
        public string? Category { get; set; }
        public string? Suburb { get; set; }
        public string? Ward { get; set; }
        public string? ContactName { get; set; }
        public string? ContactPhone { get; set; }
        public string? ContactEmail { get; set; }
        public string? LocationNotes { get; set; }
        public string? MediaMetadata { get; set; }
        public string? AudioMetadata { get; set; }
    }
}
