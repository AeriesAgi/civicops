using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CivicOps.Services
{
    public class OptionalAiProviderRegistry
    {
        public OptionalAiProviderRegistry(
            IConfiguration configuration,
            PartnerAiOptions options,
            IHttpClientFactory httpClientFactory,
            ILogger<OpenAiCompatibleProvider> providerLogger)
        {
            var aiml = new OpenAiCompatibleProvider(
                "AI/ML API",
                configuration["AIML_API_KEY"] ?? string.Empty,
                options.AimlApiBaseUrl,
                options.AimlModel,
                httpClientFactory,
                providerLogger);

            var featherless = new OpenAiCompatibleProvider(
                "Featherless AI",
                configuration["FEATHERLESS_API_KEY"] ?? string.Empty,
                options.FeatherlessApiBaseUrl,
                options.FeatherlessModel,
                httpClientFactory,
                providerLogger);

            Providers = new[] { aiml, featherless };
        }

        public IReadOnlyList<IOptionalAiProvider> Providers { get; }

        public object Status() => new
        {
            preferredProvider = Providers.FirstOrDefault(p => p.IsConfigured)?.ProviderName ?? "Local deterministic fallback",
            providers = Providers.Select(p => new
            {
                p.ProviderName,
                configured = p.IsConfigured,
                p.Model
            })
        };
    }
}
