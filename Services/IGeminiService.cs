using CivicOps.Models;
using System.Threading.Tasks;

namespace CivicOps.Services
{
    public interface IGeminiService
    {
        bool IsEnabled { get; }
        string Status { get; }
        Task<ClassificationResult> ClassifyWithGeminiAsync(string description, string? category = null);
    }
}
