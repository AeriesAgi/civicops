using CivicOps.Models;
using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CivicOps.Services
{
    public class GeminiService : IGeminiService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<GeminiService> _logger;
        private readonly IClassificationService _fallbackService;
        private readonly HttpClient _httpClient;

        public bool IsEnabled { get; private set; }
        public string Status { get; private set; } = "Not Configured";

        public GeminiService(
            IConfiguration configuration,
            ILogger<GeminiService> logger,
            DeterministicClassificationService fallbackService,
            IHttpClientFactory httpClientFactory)
        {
            _configuration = configuration;
            _logger = logger;
            _fallbackService = fallbackService;
            _httpClient = httpClientFactory.CreateClient();

            InitializeGemini();
        }

        private void InitializeGemini()
        {
            var apiKey = _configuration["GEMINI_API_KEY"];
            var enabled = _configuration.GetValue<bool>("GEMINI_ENABLED", false);

            if (!string.IsNullOrEmpty(apiKey) && enabled)
            {
                IsEnabled = true;
                Status = "Configured - Hybrid Mode";
                _logger.LogInformation("Gemini service enabled in hybrid mode");
            }
            else
            {
                IsEnabled = false;
                Status = enabled ? "Enabled but API Key Missing" : "Disabled - Using Deterministic Fallback";
                _logger.LogInformation("Gemini service disabled, using deterministic fallback");
            }
        }

        public async Task<ClassificationResult> ClassifyWithGeminiAsync(string description, string? category = null)
        {
            if (!IsEnabled)
            {
                _logger.LogInformation("Gemini disabled, using deterministic fallback");
                return await _fallbackService.ClassifyIncidentAsync(description, category);
            }

            try
            {
                var apiKey = _configuration["GEMINI_API_KEY"];
                var model = _configuration.GetValue<string>("GEMINI_MODEL", "gemini-2.0-flash-exp");

                var prompt = BuildClassificationPrompt(description, category);
                
                var requestBody = new
                {
                    contents = new[]
                    {
                        new
                        {
                            parts = new[]
                            {
                                new { text = prompt }
                            }
                        }
                    },
                    generationConfig = new
                    {
                        temperature = 0.3,
                        maxOutputTokens = 500
                    }
                };

                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}";
                var response = await _httpClient.PostAsync(url, content);

                if (response.IsSuccessStatusCode)
                {
                    var responseJson = await response.Content.ReadAsStringAsync();
                    var result = ParseGeminiResponse(responseJson, description);
                    result.IsGeminiProcessed = true;
                    result.Method = "Gemini AI";
                    _logger.LogInformation("Successfully classified incident with Gemini");
                    return result;
                }
                else
                {
                    _logger.LogWarning($"Gemini API error: {response.StatusCode}, falling back to deterministic");
                    return await _fallbackService.ClassifyIncidentAsync(description, category);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling Gemini API, falling back to deterministic");
                return await _fallbackService.ClassifyIncidentAsync(description, category);
            }
        }

        private string BuildClassificationPrompt(string description, string? category)
        {
            return $@"You are a civic operations AI assistant helping classify municipal incident reports.

Analyze this incident report and provide classification in JSON format:

Report: {description}
{(category != null ? $"Suggested Category: {category}" : "")}

Provide your response as a JSON object with these fields:
- category: A brief category name (e.g., ""Water Infrastructure"", ""Electricity"", ""Road Maintenance"")
- department: One of: WaterAndSanitation, Electricity, RoadsAndStormwater, WasteManagement, ParksAndPublicSpaces, HousingInformalSettlements, EnvironmentalHealth, DisasterManagement, FireAndRescue, MetroPolicePublicSafety, SAPSLiaisonPoliceReferral, EMSMedicalReferral, WardCouncillorWardCommittee
- priority: One of: Low, Medium, High, Urgent
- summary: A clear one-sentence summary of the incident

Respond ONLY with valid JSON, no other text.";
        }

        private ClassificationResult ParseGeminiResponse(string responseJson, string originalDescription)
        {
            try
            {
                using var doc = JsonDocument.Parse(responseJson);
                var root = doc.RootElement;
                
                if (root.TryGetProperty("candidates", out var candidates) && candidates.GetArrayLength() > 0)
                {
                    var firstCandidate = candidates[0];
                    if (firstCandidate.TryGetProperty("content", out var content) &&
                        content.TryGetProperty("parts", out var parts) && parts.GetArrayLength() > 0)
                    {
                        var text = parts[0].GetProperty("text").GetString() ?? "";
                        
                        // Extract JSON from response (might be wrapped in markdown)
                        var jsonStart = text.IndexOf('{');
                        var jsonEnd = text.LastIndexOf('}');
                        if (jsonStart >= 0 && jsonEnd > jsonStart)
                        {
                            var jsonText = text.Substring(jsonStart, jsonEnd - jsonStart + 1);
                            using var classificationDoc = JsonDocument.Parse(jsonText);
                            var classification = classificationDoc.RootElement;

                            var result = new ClassificationResult
                            {
                                Category = classification.GetProperty("category").GetString() ?? "General",
                                Summary = classification.GetProperty("summary").GetString() ?? originalDescription,
                                Method = "Gemini AI",
                                IsGeminiProcessed = true
                            };

                            // Parse department
                            var deptStr = classification.GetProperty("department").GetString();
                            if (Enum.TryParse<Department>(deptStr, out var dept))
                            {
                                result.Department = dept;
                            }
                            else
                            {
                                result.Department = Department.WardCouncillorWardCommittee;
                            }

                            // Parse priority
                            var priorityStr = classification.GetProperty("priority").GetString();
                            if (Enum.TryParse<IncidentPriority>(priorityStr, out var priority))
                            {
                                result.Priority = priority;
                            }
                            else
                            {
                                result.Priority = IncidentPriority.Medium;
                            }

                            return result;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing Gemini response");
            }

            // Fallback if parsing fails
            return new ClassificationResult
            {
                Category = "General",
                Department = Department.WardCouncillorWardCommittee,
                Priority = IncidentPriority.Medium,
                Summary = originalDescription,
                Method = "Gemini AI (Parse Failed)",
                IsGeminiProcessed = false
            };
        }
    }
}
