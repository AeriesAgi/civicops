using CivicOps.Models;
using System.Threading.Tasks;

namespace CivicOps.Services
{
    public class ClassificationResult
    {
        public string Category { get; set; } = string.Empty;
        public Department Department { get; set; }
        public IncidentPriority Priority { get; set; }
        public string Summary { get; set; } = string.Empty;
        public string Method { get; set; } = "Deterministic";
        public bool IsGeminiProcessed { get; set; }
    }

    public interface IClassificationService
    {
        Task<ClassificationResult> ClassifyIncidentAsync(string description, string? category = null);
    }
}
