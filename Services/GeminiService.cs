using CivicOps.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
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
        private static readonly string[] UnsupportedModelSignals = { "tts", "live", "audio", "image", "video", "embedding", "robot", "grounding" };
        private readonly IConfiguration _configuration;
        private readonly ILogger<GeminiService> _logger;
        private readonly IClassificationService _fallbackService;
        private readonly HttpClient _httpClient;
        private readonly object _diagnosticLock = new();
        private DateTime? _quotaCooldownUntilUtc;
        private DateTime? _manualTestCooldownUntilUtc;
        private int _callsSinceAppStart;
        private string _lastCallAction = "None";
        private string _lastModelUsed = "None";
        private string _lastResult = "No calls yet";

        public bool IsEnabled { get; private set; }
        public string Status { get; private set; } = "Not Configured";
        public string Model { get; private set; } = "gemini-2.5-flash";
        public string RoutineModel { get; private set; } = "gemini-3.1-flash-lite";
        public string Mode { get; private set; } = "Hybrid";
        public List<string> FallbackModels { get; private set; } = new();
        public int ManualTestCooldownSeconds { get; private set; } = 60;
        public int QuotaCooldownMinutes { get; private set; } = 30;

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
            _httpClient.Timeout = TimeSpan.FromSeconds(20);
            InitializeGemini();
        }

        private void InitializeGemini()
        {
            var apiKey = _configuration["GEMINI_API_KEY"];
            var enabled = _configuration.GetValue<bool?>("Gemini:Enabled") ?? _configuration.GetValue<bool>("GEMINI_ENABLED", false);
            Model = _configuration.GetValue<string>("Gemini:PremiumModel") ?? _configuration.GetValue<string>("Gemini:Model") ?? _configuration.GetValue<string>("GEMINI_MODEL", "gemini-2.5-flash") ?? "gemini-2.5-flash";
            RoutineModel = _configuration.GetValue<string>("Gemini:RoutineModel") ?? _configuration.GetValue<string>("GEMINI_ROUTINE_MODEL", "gemini-2.5-flash-lite") ?? "gemini-2.5-flash-lite";
            Mode = _configuration.GetValue<string>("Gemini:Mode") ?? _configuration.GetValue<string>("GEMINI_MODE", "Hybrid") ?? "Hybrid";
            ManualTestCooldownSeconds = _configuration.GetValue<int>("GEMINI_MANUAL_TEST_COOLDOWN_SECONDS", 60);
            QuotaCooldownMinutes = _configuration.GetValue<int>("GEMINI_QUOTA_COOLDOWN_MINUTES", 30);
            FallbackModels = (_configuration.GetValue<string>("Gemini:FallbackModels") ?? _configuration.GetValue<string>("GEMINI_FALLBACK_MODELS", "gemini-2.5-flash-lite,gemini-2.0-flash-lite,gemini-2.0-flash") ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(IsSupportedTextModel)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            IsEnabled = !string.IsNullOrWhiteSpace(apiKey) && enabled;
            Status = IsEnabled
                ? $"Configured - {Mode} Mode ({Model}; routine {RoutineModel})"
                : enabled ? "Enabled but API Key Missing - Fallback Active" : "Disabled - Local Deterministic Fallback Active";

            _logger.LogInformation("Gemini service initialized. Enabled={Enabled}, Mode={Mode}, Primary={PrimaryModel}, Routine={RoutineModel}", IsEnabled, Mode, Model, RoutineModel);
        }

        public GeminiDiagnostics GetDiagnostics()
        {
            lock (_diagnosticLock)
            {
                var quotaLimited = _quotaCooldownUntilUtc.HasValue && _quotaCooldownUntilUtc.Value > DateTime.UtcNow;
                return new GeminiDiagnostics
                {
                    Enabled = IsEnabled,
                    KeyPresent = !string.IsNullOrWhiteSpace(_configuration["GEMINI_API_KEY"]),
                    PrimaryModel = Model,
                    RoutineModel = RoutineModel,
                    FallbackModels = FallbackModels,
                    Mode = Mode,
                    CallsSinceAppStart = _callsSinceAppStart,
                    LastCallAction = _lastCallAction,
                    LastModelUsed = _lastModelUsed,
                    LastResult = _lastResult,
                    QuotaLimited = quotaLimited,
                    FallbackActive = !IsEnabled || quotaLimited || !_lastResult.Contains("succeeded", StringComparison.OrdinalIgnoreCase),
                    ManualTestCooldownSeconds = ManualTestCooldownSeconds,
                    QuotaCooldownMinutes = QuotaCooldownMinutes
                };
            }
        }

        public async Task<GeminiHealthResult> TestConnectionAsync()
        {
            if (!IsEnabled)
            {
                RecordResult("manual-health-test", "None", "Skipped: Gemini disabled or key missing");
                return new GeminiHealthResult { Success = false, Status = Status, Model = Model, Mode = Mode, Message = "Gemini is disabled or GEMINI_API_KEY is missing; local deterministic fallback is active." };
            }

            if (_manualTestCooldownUntilUtc.HasValue && _manualTestCooldownUntilUtc.Value > DateTime.UtcNow)
            {
                return new GeminiHealthResult { Success = false, Status = "Manual Gemini test cooldown active", Model = Model, Mode = Mode, Message = $"Quota-safe cooldown is active. Try again after {_manualTestCooldownUntilUtc.Value:O}." };
            }

            _manualTestCooldownUntilUtc = DateTime.UtcNow.AddSeconds(Math.Max(ManualTestCooldownSeconds, 1));
            var result = await ClassifyWithGeminiAsync("Blocked storm drain causing flooding on a public road.", null, "manual-health-test");
            return new GeminiHealthResult
            {
                Success = result.IsGeminiProcessed,
                Status = result.IsGeminiProcessed ? "Live Gemini call succeeded" : "Gemini fallback used",
                Model = result.IsGeminiProcessed ? result.Method.Replace("Gemini ", string.Empty) : Model,
                Mode = Mode,
                Message = result.IsGeminiProcessed
                    ? $"Gemini returned category '{result.Category}' and department '{result.Department}'."
                    : "Live Gemini was unavailable, unsupported, quota-limited, or unparseable; local deterministic fallback remains active."
            };
        }

        public async Task<ClassificationResult> ClassifyWithGeminiAsync(string description, string? category = null, string action = "routine-classification")
        {
            if (!IsEnabled || IsQuotaCoolingDown())
            {
                RecordResult(action, "Local deterministic fallback", "Fallback used before live Gemini call");
                var localFallback = await _fallbackService.ClassifyIncidentAsync(description, category);
                localFallback.Method = "Local deterministic fallback";
                localFallback.IsGeminiProcessed = false;
                return localFallback;
            }

            foreach (var candidateModel in BuildModelPlan(action))
            {
                if (!IsSupportedTextModel(candidateModel))
                {
                    RecordResult(action, candidateModel, "Skipped unsupported model");
                    continue;
                }

                var callResult = await TryCallGeminiAsync(candidateModel, description, category, action);
                if (callResult.Completed)
                {
                    return callResult.Result;
                }

                if (callResult.QuotaLimited)
                {
                    StartQuotaCooldown(action, candidateModel);
                    break;
                }
            }

            var fallback = await _fallbackService.ClassifyIncidentAsync(description, category);
            fallback.Method = "Local deterministic fallback";
            fallback.IsGeminiProcessed = false;
            RecordResult(action, "Local deterministic fallback", "Fallback active after Gemini model plan");
            return fallback;
        }

        private IEnumerable<string> BuildModelPlan(string action)
        {
            var models = new List<string>();
            if (action.Contains("judge", StringComparison.OrdinalIgnoreCase) || action.Contains("summary", StringComparison.OrdinalIgnoreCase) || action.Contains("health", StringComparison.OrdinalIgnoreCase))
            {
                models.Add(Model);
            }
            else
            {
                models.Add(RoutineModel);
                models.Add(Model);
            }

            models.AddRange(FallbackModels);
            return models.Where(m => !string.IsNullOrWhiteSpace(m)).Distinct(StringComparer.OrdinalIgnoreCase);
        }

        private async Task<(bool Completed, bool QuotaLimited, ClassificationResult Result)> TryCallGeminiAsync(string model, string description, string? category, string action)
        {
            try
            {
                var apiKey = _configuration["GEMINI_API_KEY"];
                var requestBody = new
                {
                    contents = new[] { new { parts = new[] { new { text = BuildClassificationPrompt(description, category) } } } },
                    generationConfig = new { temperature = 0.2, maxOutputTokens = 500 }
                };

                var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
                var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}";
                RecordCall(action, model);
                var response = await _httpClient.PostAsync(url, content);

                if (response.IsSuccessStatusCode)
                {
                    var responseJson = await response.Content.ReadAsStringAsync();
                    var result = ParseGeminiResponse(responseJson, description, model, action);
                    if (result.IsGeminiProcessed)
                    {
                        RecordResult(action, model, "Gemini call succeeded");
                        return (true, false, result);
                    }

                    RecordResult(action, model, "Gemini response parse failed");
                    return (false, false, result);
                }

                var quota = response.StatusCode == HttpStatusCode.TooManyRequests || (int)response.StatusCode == 429;
                RecordResult(action, model, quota ? "Gemini quota-limited" : $"Gemini API error {(int)response.StatusCode}");
                return (false, quota, new ClassificationResult { Method = $"Gemini {model} unavailable", Summary = description, Category = category ?? "General" });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Gemini model {Model} failed once for action {Action}; no retry loop will run.", model, action);
                RecordResult(action, model, "Gemini call exception; fallback pending");
                return (false, false, new ClassificationResult { Method = $"Gemini {model} exception", Summary = description, Category = category ?? "General" });
            }
        }

        private bool IsQuotaCoolingDown() => _quotaCooldownUntilUtc.HasValue && _quotaCooldownUntilUtc.Value > DateTime.UtcNow;

        private void StartQuotaCooldown(string action, string model)
        {
            _quotaCooldownUntilUtc = DateTime.UtcNow.AddMinutes(Math.Max(QuotaCooldownMinutes, 1));
            RecordResult(action, model, $"Quota-limited; cooldown active until {_quotaCooldownUntilUtc.Value:O}");
        }

        private static bool IsSupportedTextModel(string model)
        {
            var normalized = model.ToLowerInvariant();
            return normalized.StartsWith("gemini-") && !UnsupportedModelSignals.Any(normalized.Contains) && !normalized.Contains("0/0");
        }

        private void RecordCall(string action, string model)
        {
            lock (_diagnosticLock)
            {
                _callsSinceAppStart++;
                _lastCallAction = action;
                _lastModelUsed = model;
                _lastResult = "Call in progress";
            }
        }

        private void RecordResult(string action, string model, string result)
        {
            lock (_diagnosticLock)
            {
                _lastCallAction = action;
                _lastModelUsed = model;
                _lastResult = result;
            }
        }

        private string BuildClassificationPrompt(string description, string? category)
        {
            return $@"You are CivicOps, a human-in-the-loop civic operations AI assistant. Classify only this reported civic issue; do not claim official authority or replace emergency services.

Report: {description}
{(category != null ? $"Suggested Category: {category}" : "")}

Return only valid JSON with: category, department, priority, summary.
Department must be one of: WaterAndSanitation, Electricity, RoadsAndStormwater, WasteManagement, ParksAndPublicSpaces, HousingInformalSettlements, EnvironmentalHealth, DisasterManagement, FireAndRescue, MetroPolicePublicSafety, SAPSLiaisonPoliceReferral, EMSMedicalReferral, WardCouncillorWardCommittee.
Priority must be one of: Low, Medium, High, Urgent.";
        }

        private ClassificationResult ParseGeminiResponse(string responseJson, string originalDescription, string model, string action)
        {
            try
            {
                using var doc = JsonDocument.Parse(responseJson);
                if (doc.RootElement.TryGetProperty("candidates", out var candidates) && candidates.GetArrayLength() > 0 &&
                    candidates[0].TryGetProperty("content", out var content) &&
                    content.TryGetProperty("parts", out var parts) && parts.GetArrayLength() > 0)
                {
                    var text = parts[0].GetProperty("text").GetString() ?? string.Empty;
                    var jsonStart = text.IndexOf('{');
                    var jsonEnd = text.LastIndexOf('}');
                    if (jsonStart >= 0 && jsonEnd > jsonStart)
                    {
                        using var classificationDoc = JsonDocument.Parse(text.Substring(jsonStart, jsonEnd - jsonStart + 1));
                        var classification = classificationDoc.RootElement;
                        var result = new ClassificationResult
                        {
                            Category = classification.GetProperty("category").GetString() ?? "General civic issue",
                            Summary = classification.GetProperty("summary").GetString() ?? originalDescription,
                            Method = action.Contains("judge", StringComparison.OrdinalIgnoreCase) ? $"Gemini premium: {model}" : model.Equals(RoutineModel, StringComparison.OrdinalIgnoreCase) ? $"Gemini routine: {model}" : $"Gemini fallback: {model}",
                            IsGeminiProcessed = true,
                            Department = Department.WardCouncillorWardCommittee,
                            Priority = IncidentPriority.Medium
                        };

                        if (classification.TryGetProperty("department", out var deptProp) && Enum.TryParse<Department>(deptProp.GetString(), out var dept)) result.Department = dept;
                        if (classification.TryGetProperty("priority", out var priorityProp) && Enum.TryParse<IncidentPriority>(priorityProp.GetString(), out var priority)) result.Priority = priority;
                        return result;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error parsing Gemini response from {Model}", model);
            }

            return new ClassificationResult { Category = "General", Department = Department.WardCouncillorWardCommittee, Priority = IncidentPriority.Medium, Summary = originalDescription, Method = $"Gemini {model} parse failed", IsGeminiProcessed = false };
        }
    }
}
