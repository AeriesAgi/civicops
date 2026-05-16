using CivicOps.Models;
using CivicOps.Services;
using Microsoft.AspNetCore.Mvc;
using System;
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

        public ApiController(
            IDataService dataService,
            IGeminiService geminiService,
            IClassificationService classificationService,
            IIncidentIntakeService intakeService,
            IWhatsAppService whatsAppService)
        {
            _dataService = dataService;
            _geminiService = geminiService;
            _classificationService = classificationService;
            _intakeService = intakeService;
            _whatsAppService = whatsAppService;
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
                    mode = _geminiService.IsEnabled ? "Configured" : "Demo",
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
                    mode = "Placeholder",
                    description = "Audio-to-text transcription service"
                },
                new
                {
                    name = "SMS Notifications",
                    status = "Future Connector",
                    mode = "Placeholder",
                    description = "SMS notification service"
                },
                new
                {
                    name = "Email Notifications",
                    status = "Future Connector",
                    mode = "Placeholder",
                    description = "Email notification service"
                },
                new
                {
                    name = "GIS/Geocoding",
                    status = "Future Connector",
                    mode = "Placeholder",
                    description = "Location mapping and geocoding"
                },
                new
                {
                    name = "Municipal ERP",
                    status = "Future Connector",
                    mode = "Placeholder",
                    description = "Integration with municipal ticketing systems"
                }
            };

            return Ok(new { success = true, connectors });
        }
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
}
