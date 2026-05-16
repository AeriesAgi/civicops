using CivicOps.Models;
using CivicOps.Services;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace CivicOps.Controllers
{
    [Route("webhooks/whatsapp")]
    [ApiController]
    public class WhatsAppController : ControllerBase
    {
        private readonly IIncidentIntakeService _intakeService;
        private readonly IWhatsAppService _whatsAppService;

        public WhatsAppController(IIncidentIntakeService intakeService, IWhatsAppService whatsAppService)
        {
            _intakeService = intakeService;
            _whatsAppService = whatsAppService;
        }

        [HttpGet]
        public IActionResult VerifyWebhook([FromQuery(Name = "hub.mode")] string mode,
                                          [FromQuery(Name = "hub.verify_token")] string token,
                                          [FromQuery(Name = "hub.challenge")] string challenge)
        {
            if (_whatsAppService.VerifyToken(mode, token))
            {
                return Content(challenge ?? string.Empty, "text/plain");
            }

            return Forbid();
        }

        [HttpPost]
        public async Task<IActionResult> ReceiveMessage([FromBody] WhatsAppWebhookPayload payload)
        {
            var processed = 0;
            var references = new List<string>();

            if (payload?.Entry != null)
            {
                foreach (var entry in payload.Entry)
                {
                    if (entry.Changes == null)
                    {
                        continue;
                    }

                    foreach (var change in entry.Changes)
                    {
                        if (change.Value?.Messages == null)
                        {
                            continue;
                        }

                        foreach (var message in change.Value.Messages)
                        {
                            var result = await ProcessWhatsAppMessage(message);
                            if (result != null)
                            {
                                processed++;
                                references.Add(result.Incident.ReferenceNumber);
                            }
                        }
                    }
                }
            }

            return Ok(new { success = true, processed, references });
        }

        private async Task<IncidentIntakeResult?> ProcessWhatsAppMessage(WhatsAppMessage message)
        {
            if (message.Type != "text" || string.IsNullOrWhiteSpace(message.Text?.Body))
            {
                return null;
            }

            var result = await _intakeService.ProcessAsync(new IncidentIntakeRequest
            {
                SourceChannel = SourceChannel.WhatsApp,
                Description = message.Text.Body,
                CreatedBy = "WhatsApp Webhook",
                ConnectorMetadata = new Dictionary<string, string>
                {
                    ["whatsapp_demo"] = "false",
                    ["whatsapp_from_masked"] = _whatsAppService.MaskPhone(message.From),
                    ["whatsapp_message_id"] = message.Id ?? "unknown"
                }
            });

            if (!string.IsNullOrWhiteSpace(message.From))
            {
                await _whatsAppService.SendTextAsync(message.From, result.CitizenResponse);
            }

            return result;
        }
    }

    public class WhatsAppWebhookPayload
    {
        [JsonPropertyName("object")]
        public string? Object { get; set; }
        [JsonPropertyName("entry")]
        public WhatsAppEntry[]? Entry { get; set; }
    }

    public class WhatsAppEntry
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }
        [JsonPropertyName("changes")]
        public WhatsAppChange[]? Changes { get; set; }
    }

    public class WhatsAppChange
    {
        [JsonPropertyName("value")]
        public WhatsAppValue? Value { get; set; }
        [JsonPropertyName("field")]
        public string? Field { get; set; }
    }

    public class WhatsAppValue
    {
        [JsonPropertyName("messaging_product")]
        public string? MessagingProduct { get; set; }
        [JsonPropertyName("messages")]
        public WhatsAppMessage[]? Messages { get; set; }
    }

    public class WhatsAppMessage
    {
        [JsonPropertyName("from")]
        public string? From { get; set; }
        [JsonPropertyName("id")]
        public string? Id { get; set; }
        [JsonPropertyName("type")]
        public string? Type { get; set; }
        [JsonPropertyName("text")]
        public WhatsAppTextMessage? Text { get; set; }
        [JsonPropertyName("audio")]
        public WhatsAppAudioMessage? Audio { get; set; }
    }

    public class WhatsAppTextMessage
    {
        [JsonPropertyName("body")]
        public string? Body { get; set; }
    }

    public class WhatsAppAudioMessage
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }
        [JsonPropertyName("mime_type")]
        public string? MimeType { get; set; }
    }
}
