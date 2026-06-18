using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace CivicOps.Services
{
    public class OpenAiCompatibleProvider : IOptionalAiProvider
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<OpenAiCompatibleProvider> _logger;
        private readonly string _apiKey;
        private readonly string _baseUrl;

        public OpenAiCompatibleProvider(
            string providerName,
            string apiKey,
            string baseUrl,
            string model,
            IHttpClientFactory httpClientFactory,
            ILogger<OpenAiCompatibleProvider> logger)
        {
            ProviderName = providerName;
            _apiKey = apiKey;
            _baseUrl = string.IsNullOrWhiteSpace(baseUrl) ? "https://api.openai.com/v1" : baseUrl.TrimEnd('/');
            Model = model;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public string ProviderName { get; }
        public bool IsConfigured => !string.IsNullOrWhiteSpace(_apiKey);
        public string Model { get; }

        public async Task<string?> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken = default)
        {
            if (!IsConfigured)
            {
                return null;
            }

            try
            {
                var client = _httpClientFactory.CreateClient("optional-ai");
                client.Timeout = TimeSpan.FromSeconds(20);
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

                var payload = new
                {
                    model = Model,
                    messages = new[]
                    {
                        new { role = "system", content = systemPrompt },
                        new { role = "user", content = userPrompt }
                    },
                    temperature = 0.2,
                    max_tokens = 700
                };

                using var response = await client.PostAsJsonAsync($"{_baseUrl}/chat/completions", payload, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("{Provider} optional AI call returned {Status}; deterministic fallback remains active.", ProviderName, response.StatusCode);
                    return null;
                }

                using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
                return json.RootElement
                    .GetProperty("choices")[0]
                    .GetProperty("message")
                    .GetProperty("content")
                    .GetString();
            }
            catch (Exception ex)
            {
                _logger.LogInformation(ex, "{Provider} optional AI call skipped; deterministic fallback remains active.", ProviderName);
                return null;
            }
        }
    }
}
