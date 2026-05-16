using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace CivicOps.Services
{
    public class WhatsAppService : IWhatsAppService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<WhatsAppService> _logger;
        private readonly HttpClient _httpClient;

        public WhatsAppService(IConfiguration configuration, ILogger<WhatsAppService> logger, IHttpClientFactory httpClientFactory)
        {
            _configuration = configuration;
            _logger = logger;
            _httpClient = httpClientFactory.CreateClient();
        }

        public WhatsAppStatus GetStatus()
        {
            var enabled = _configuration.GetValue<bool>("WHATSAPP_ENABLED", false);
            var demoMode = _configuration.GetValue<bool>("WHATSAPP_DEMO_MODE", true);
            var accessToken = _configuration["WHATSAPP_ACCESS_TOKEN"];
            var phoneNumberId = _configuration["WHATSAPP_PHONE_NUMBER_ID"];
            var verifyToken = _configuration["WHATSAPP_VERIFY_TOKEN"];
            var graphVersion = _configuration.GetValue<string>("WHATSAPP_GRAPH_VERSION", "v22.0") ?? "v22.0";
            var publicBaseUrl = _configuration.GetValue<string>("WHATSAPP_PUBLIC_BASE_URL", string.Empty) ?? string.Empty;
            var canSend = enabled && !demoMode && !string.IsNullOrWhiteSpace(accessToken) && !string.IsNullOrWhiteSpace(phoneNumberId);
            var webhookReady = !string.IsNullOrWhiteSpace(verifyToken) && !string.IsNullOrWhiteSpace(publicBaseUrl);

            return new WhatsAppStatus
            {
                Enabled = enabled,
                DemoMode = demoMode,
                CanSend = canSend,
                GraphVersion = graphVersion,
                PublicBaseUrl = publicBaseUrl,
                Mode = canSend ? "Live Ready" : webhookReady ? "Webhook Ready" : enabled ? "Sandbox Active" : "Needs Environment Variables",
                Status = canSend
                    ? "Live Ready — outbound send configured"
                    : webhookReady
                        ? "Webhook Ready — verification configured; outbound send waits for live credentials"
                        : "Sandbox Active — configure Meta credentials and public URL for live readiness"
            };
        }

        public bool VerifyToken(string mode, string token)
        {
            var verifyToken = _configuration["WHATSAPP_VERIFY_TOKEN"];
            return mode == "subscribe" && !string.IsNullOrWhiteSpace(verifyToken) && token == verifyToken;
        }

        public async Task<bool> SendTextAsync(string to, string message)
        {
            var status = GetStatus();
            if (!status.CanSend)
            {
                _logger.LogInformation("WhatsApp outbound skipped for {MaskedPhone}; connector is not in live send mode", MaskPhone(to));
                return false;
            }

            var accessToken = _configuration["WHATSAPP_ACCESS_TOKEN"];
            var phoneNumberId = _configuration["WHATSAPP_PHONE_NUMBER_ID"];
            var payload = new
            {
                messaging_product = "whatsapp",
                recipient_type = "individual",
                to,
                type = "text",
                text = new { preview_url = false, body = message }
            };

            var request = new HttpRequestMessage(HttpMethod.Post, $"https://graph.facebook.com/{status.GraphVersion}/{phoneNumberId}/messages");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("WhatsApp send failed with status {StatusCode} for {MaskedPhone}", response.StatusCode, MaskPhone(to));
                return false;
            }

            _logger.LogInformation("WhatsApp response sent to {MaskedPhone}", MaskPhone(to));
            return true;
        }

        public string MaskPhone(string? phoneNumber)
        {
            if (string.IsNullOrWhiteSpace(phoneNumber))
            {
                return "unknown";
            }

            var digits = new string(phoneNumber.Where(char.IsDigit).ToArray());
            if (digits.Length <= 4)
            {
                return "****";
            }

            return $"***{digits[^4..]}";
        }
    }
}
