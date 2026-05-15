using CivicOps.Models;
using CivicOps.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System;
using System.Threading.Tasks;

namespace CivicOps.Controllers
{
    [ApiController]
    [Route("webhooks/whatsapp")]
    public class WhatsAppController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly IDataService _dataService;
        private readonly IGeminiService _geminiService;
        private readonly IClassificationService _classificationService;

        public WhatsAppController(
            IConfiguration configuration,
            IDataService dataService,
            IGeminiService geminiService,
            IClassificationService classificationService)
        {
            _configuration = configuration;
            _dataService = dataService;
            _geminiService = geminiService;
            _classificationService = classificationService;
        }

        [HttpGet]
        public IActionResult VerifyWebhook([FromQuery(Name = "hub.mode")] string mode,
                                          [FromQuery(Name = "hub.verify_token")] string token,
                                          [FromQuery(Name = "hub.challenge")] string challenge)
        {
            var verifyToken = _configuration["WHATSAPP_VERIFY_TOKEN"] ?? "civicops_verify_token_2026";

            if (mode == "subscribe" && token == verifyToken)
            {
                return Ok(challenge);
            }

            return Forbid();
        }

        [HttpPost]
        public async Task<IActionResult> ReceiveMessage([FromBody] WhatsAppWebhookPayload payload)
        {
            try
            {
                // In demo mode, we accept simplified payloads
                // In production, this would parse the full WhatsApp Cloud API webhook format
                
                if (payload?.Entry != null && payload.Entry.Length > 0)
                {
                    foreach (var entry in payload.Entry)
                    {
                        if (entry.Changes != null && entry.Changes.Length > 0)
                        {
                            foreach (var change in entry.Changes)
                            {
                                if (change.Value?.Messages != null && change.Value.Messages.Length > 0)
                                {
                                    foreach (var message in change.Value.Messages)
                                    {
                                        await ProcessWhatsAppMessage(message);
                                    }
                                }
                            }
                        }
                    }
                }

                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        private async Task ProcessWhatsAppMessage(WhatsAppMessage message)
        {
            if (message.Type == "text" && !string.IsNullOrEmpty(message.Text?.Body))
            {
                var description = message.Text.Body;

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
                    SourceChannel = SourceChannel.WhatsApp,
                    Description = description,
                    AISummary = classification.Summary,
                    Category = classification.Category,
                    AssignedDepartment = classification.Department,
                    Suburb = "Unknown",
                    Ward = "Unknown",
                    Priority = classification.Priority,
                    IsGeminiProcessed = classification.IsGeminiProcessed,
                    ClassificationMethod = classification.Method
                };

                incident.ConnectorMetadata["whatsapp_from"] = message.From ?? "unknown";
                incident.ConnectorMetadata["whatsapp_message_id"] = message.Id ?? "unknown";

                incident.InternalNotes.Add(new IncidentNote
                {
                    Content = $"Incident created via WhatsApp. Classified as {classification.Category} using {classification.Method}.",
                    IsPublic = false
                });

                incident.PublicUpdates.Add($"Your report has been received. Reference: {incident.ReferenceNumber}");

                await _dataService.SaveIncidentAsync(incident);

                // In production, would send WhatsApp reply here
                // await SendWhatsAppReply(message.From, $"Thank you! Your reference number is {incident.ReferenceNumber}");
            }
        }
    }

    // WhatsApp webhook payload models (simplified for demo)
    public class WhatsAppWebhookPayload
    {
        public string? Object { get; set; }
        public WhatsAppEntry[]? Entry { get; set; }
    }

    public class WhatsAppEntry
    {
        public string? Id { get; set; }
        public WhatsAppChange[]? Changes { get; set; }
    }

    public class WhatsAppChange
    {
        public WhatsAppValue? Value { get; set; }
        public string? Field { get; set; }
    }

    public class WhatsAppValue
    {
        public string? MessagingProduct { get; set; }
        public WhatsAppMessage[]? Messages { get; set; }
    }

    public class WhatsAppMessage
    {
        public string? From { get; set; }
        public string? Id { get; set; }
        public string? Type { get; set; }
        public WhatsAppTextMessage? Text { get; set; }
        public WhatsAppAudioMessage? Audio { get; set; }
    }

    public class WhatsAppTextMessage
    {
        public string? Body { get; set; }
    }

    public class WhatsAppAudioMessage
    {
        public string? Id { get; set; }
        public string? MimeType { get; set; }
    }
}
