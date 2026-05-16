using CivicOps.Models;
using CivicOps.Services;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CivicOps.Controllers
{
    [Route("demo")]
    public class DemoController : Controller
    {
        private readonly IIncidentIntakeService _intakeService;
        private readonly IWhatsAppService _whatsAppService;

        public DemoController(IIncidentIntakeService intakeService, IWhatsAppService whatsAppService)
        {
            _intakeService = intakeService;
            _whatsAppService = whatsAppService;
        }

        [HttpGet("whatsapp")]
        [HttpGet("/Demo/WhatsAppSimulator")]
        public IActionResult WhatsAppSimulator()
        {
            return View();
        }

        [HttpPost("whatsapp/inbound")]
        public async Task<IActionResult> SimulateWhatsAppInbound([FromBody] WhatsAppSimulation simulation)
        {
            try
            {
                var result = await _intakeService.ProcessAsync(new IncidentIntakeRequest
                {
                    SourceChannel = SourceChannel.WhatsApp,
                    Description = simulation.Message,
                    Suburb = simulation.Suburb,
                    Ward = simulation.Ward,
                    ContactPhone = simulation.PhoneNumber,
                    CreatedBy = "WhatsApp Demo",
                    ConnectorMetadata = new Dictionary<string, string>
                    {
                        ["whatsapp_demo"] = "true",
                        ["whatsapp_sender"] = simulation.SenderName ?? "Demo User",
                        ["whatsapp_masked_phone"] = _whatsAppService.MaskPhone(simulation.PhoneNumber)
                    }
                });

                var incident = result.Incident;
                return Ok(new
                {
                    success = true,
                    referenceNumber = incident.ReferenceNumber,
                    validation = result.Validation,
                    department = incident.AssignedDepartment.GetDisplayName(),
                    category = incident.Category,
                    priority = incident.Priority.ToString(),
                    citizenResponse = result.CitizenResponse,
                    alertRecommendation = result.AlertRecommendation,
                    message = $"Thank you! Your reference number is {incident.ReferenceNumber}. The {incident.AssignedDepartment.GetDisplayName()} department has been notified."
                });
            }
            catch (System.Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpGet("voicenote")]
        [HttpGet("/Demo/VoiceNoteSimulator")]
        public IActionResult VoiceNoteSimulator()
        {
            return View();
        }

        [HttpPost("voicenote/submit")]
        public async Task<IActionResult> SimulateVoiceNote([FromBody] VoiceNoteSimulation simulation)
        {
            try
            {
                var result = await _intakeService.ProcessAsync(new IncidentIntakeRequest
                {
                    SourceChannel = SourceChannel.VoiceNote,
                    Description = simulation.Transcript,
                    Suburb = simulation.Suburb,
                    Ward = simulation.Ward,
                    AudioMetadata = $"demo-voice-{System.Guid.NewGuid()}.mp3",
                    CreatedBy = "Voice System",
                    ConnectorMetadata = new Dictionary<string, string>
                    {
                        ["voice_demo"] = "true",
                        ["audio_duration"] = simulation.DurationSeconds?.ToString() ?? "unknown"
                    }
                });

                var incident = result.Incident;
                return Ok(new
                {
                    success = true,
                    referenceNumber = incident.ReferenceNumber,
                    validation = result.Validation,
                    department = incident.AssignedDepartment.GetDisplayName(),
                    category = incident.Category,
                    priority = incident.Priority.ToString(),
                    transcript = simulation.Transcript,
                    citizenResponse = result.CitizenResponse,
                    alertRecommendation = result.AlertRecommendation,
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
