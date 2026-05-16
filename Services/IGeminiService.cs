using CivicOps.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CivicOps.Services
{
    public class GeminiHealthResult
    {
        public bool Success { get; set; }
        public string Status { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public string Mode { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }

    public class GeminiDiagnostics
    {
        public bool Enabled { get; set; }
        public bool KeyPresent { get; set; }
        public string PrimaryModel { get; set; } = string.Empty;
        public string RoutineModel { get; set; } = string.Empty;
        public IReadOnlyList<string> FallbackModels { get; set; } = new List<string>();
        public string Mode { get; set; } = string.Empty;
        public int CallsSinceAppStart { get; set; }
        public string LastCallAction { get; set; } = "None";
        public string LastModelUsed { get; set; } = "None";
        public string LastResult { get; set; } = "No calls yet";
        public bool QuotaLimited { get; set; }
        public bool FallbackActive { get; set; }
        public int ManualTestCooldownSeconds { get; set; }
        public int QuotaCooldownMinutes { get; set; }
    }

    public interface IGeminiService
    {
        bool IsEnabled { get; }
        string Status { get; }
        string Model { get; }
        string RoutineModel { get; }
        string Mode { get; }
        GeminiDiagnostics GetDiagnostics();
        Task<GeminiHealthResult> TestConnectionAsync();
        Task<ClassificationResult> ClassifyWithGeminiAsync(string description, string? category = null, string action = "routine-classification");
    }
}
