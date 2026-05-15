using CivicOps.Models;
using CivicOps.Services;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace CivicOps.Controllers
{
    [Route("demo")]
    public class DemoController : Controller
    {
        private readonly IDataService _dataService;
        private readonly IGeminiService _geminiService;
        private readonly IClassificationService _classificationService;

        public DemoController(
            IDataService dataService,
            IGeminiService geminiService,
            IClassificationService classificationService)
        {
            _dataService = dataService;
            _geminiService = geminiService;
            _classificationService = classificationService;
        }

        [HttpGet("whatsapp")]
        public IActionResult WhatsAppSimulator()
        {
            return View();
        }

        [HttpPost("whatsapp/inbound")]
        public async Task<IActionResult> SimulateWhatsAppInbound([FromBody] WhatsAppSimulation simulation)
        {
            try
            {
                // Classify the incident
                ClassificationResult classification;
                if (_geminiService.IsEnabled)
                {
                    classification = await _geminiService.ClassifyWithGeminiAsync(simulation.Message);
                }
                else
                {
                    classification = await _classificationService.ClassifyIncidentAsync(simulation.Message);
                }

                // Create incident
                var incident = new Incident
                {
                    SourceChannel = SourceChannel.WhatsApp,
                    Description = simulation.Message,
                    AISummary = classification.Summary,
                    Category = classification.Category,
                    AssignedDepartment = classification.Department,
                    Suburb = simulation.Suburb ?? "Unknown",
                    Ward = simulation.Ward ?? "Unknown",
                    Priority = classification.Priority,
                    ContactPhone = simulation.PhoneNumber,
                    IsGeminiProcessed = classification.IsGeminiProcessed,
                    ClassificationMethod = classification.Method
                };

                incident.ConnectorMetadata["whatsapp_demo"] = "true";
                incident.ConnectorMetadata["whatsapp_sender"] = simulation.SenderName ?? "Demo User";

                incident.InternalNotes.Add(new IncidentNote
                {
                    Content = $"Demo WhatsApp incident. Classified as {classification.Category} using {classification.Method}.",
                    IsPublic = false
                });

                incident.PublicUpdates.Add(new PublicUpdate 
                { 
                    Content = $"Your report has been received via WhatsApp. Reference: {incident.ReferenceNumber}",
                    UpdatedBy = "WhatsApp Demo"
                });

                await _dataService.SaveIncidentAsync(incident);

                return Ok(new
                {
                    success = true,
                    referenceNumber = incident.ReferenceNumber,
                    department = incident.AssignedDepartment.GetDisplayName(),
                    category = incident.Category,
                    priority = incident.Priority.ToString(),
                    message = $"Thank you! Your reference number is {incident.ReferenceNumber}. The {incident.AssignedDepartment.GetDisplayName()} department has been notified."
                });
            }
            catch (System.Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpGet("voicenote")]
        public IActionResult VoiceNoteSimulator()
        {
            return View();
        }

        [HttpPost("voicenote/submit")]
        public async Task<IActionResult> SimulateVoiceNote([FromBody] VoiceNoteSimulation simulation)
        {
            try
            {
                // In a real system, this would transcribe audio
                // For demo, we accept a transcript
                var description = simulation.Transcript;

                // Classify the incident
                ClassificationResult classification;
                if (_geminiService.IsEnabled)
                {
                    classification = await _geminiService.ClassifyWithGeminiAsync(description);
                }
                else
                {
                    classification = await _classificationService.ClassifyIncidentAsync(description);
                }

                // Create incident
                var incident = new Incident
                {
                    SourceChannel = SourceChannel.VoiceNote,
                    Description = description,
                    AISummary = classification.Summary,
                    Category = classification.Category,
                    AssignedDepartment = classification.Department,
                    Suburb = simulation.Suburb ?? "Unknown",
                    Ward = simulation.Ward ?? "Unknown",
                    Priority = classification.Priority,
                    AudioMetadata = $"demo-voice-{System.Guid.NewGuid()}.mp3",
                    IsGeminiProcessed = classification.IsGeminiProcessed,
                    ClassificationMethod = classification.Method
                };

                incident.ConnectorMetadata["voice_demo"] = "true";
                incident.ConnectorMetadata["audio_duration"] = simulation.DurationSeconds?.ToString() ?? "unknown";

                incident.InternalNotes.Add(new IncidentNote
                {
                    Content = $"Demo voice note incident. Classified as {classification.Category} using {classification.Method}.",
                    IsPublic = false
                });

                incident.PublicUpdates.Add(new PublicUpdate 
                { 
                    Content = $"Your voice report has been received and transcribed. Reference: {incident.ReferenceNumber}",
                    UpdatedBy = "Voice System"
                });

                await _dataService.SaveIncidentAsync(incident);

                return Ok(new
                {
                    success = true,
                    referenceNumber = incident.ReferenceNumber,
                    department = incident.AssignedDepartment.GetDisplayName(),
                    category = incident.Category,
                    priority = incident.Priority.ToString(),
                    transcript = description,
                    message = "Voice note processed successfully"
                });
            }
            catch (System.Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }
    }

    public class WhatsAppSimulation
    {
        public string Message { get; set; } = string.Empty;
        public string? SenderName { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Suburb { get; set; }
        public string? Ward { get; set; }
    }

    public class VoiceNoteSimulation
    {
        public string Transcript { get; set; } = string.Empty;
        public string? Suburb { get; set; }
        public string? Ward { get; set; }
        public int? DurationSeconds { get; set; }
    }
}
