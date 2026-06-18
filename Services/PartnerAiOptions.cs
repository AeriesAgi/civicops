using Microsoft.Extensions.Configuration;

namespace CivicOps.Services
{
    public class PartnerAiOptions
    {
        public string AimlApiBaseUrl { get; set; } = "https://api.aimlapi.com/v1";
        public string AimlModel { get; set; } = "gpt-4o-mini";
        public bool AimlApiKeyConfigured { get; set; }

        public string FeatherlessApiBaseUrl { get; set; } = "https://api.featherless.ai/v1";
        public string FeatherlessModel { get; set; } = "meta-llama/Meta-Llama-3.1-8B-Instruct";
        public bool FeatherlessApiKeyConfigured { get; set; }

        public string PreferredProvider =>
            AimlApiKeyConfigured ? "AI/ML API" :
            FeatherlessApiKeyConfigured ? "Featherless AI" :
            "Local deterministic fallback";

        public static PartnerAiOptions FromConfiguration(IConfiguration configuration)
        {
            return new PartnerAiOptions
            {
                AimlApiBaseUrl = configuration["AIML_API_BASE_URL"] ?? "https://api.aimlapi.com/v1",
                AimlModel = configuration["AIML_MODEL"] ?? "gpt-4o-mini",
                AimlApiKeyConfigured = !string.IsNullOrWhiteSpace(configuration["AIML_API_KEY"]),
                FeatherlessApiBaseUrl = "https://api.featherless.ai/v1",
                FeatherlessModel = configuration["FEATHERLESS_MODEL"] ?? "meta-llama/Meta-Llama-3.1-8B-Instruct",
                FeatherlessApiKeyConfigured = !string.IsNullOrWhiteSpace(configuration["FEATHERLESS_API_KEY"])
            };
        }
    }
}
