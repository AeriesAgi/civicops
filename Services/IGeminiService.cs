using CivicOps.Models;
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

    public interface IGeminiService
    {
        bool IsEnabled { get; }
        string Status { get; }
        string Model { get; }
        string Mode { get; }
        Task<GeminiHealthResult> TestConnectionAsync();
        Task<ClassificationResult> ClassifyWithGeminiAsync(string description, string? category = null);
    }
}
