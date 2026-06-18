using System.Threading;
using System.Threading.Tasks;

namespace CivicOps.Services
{
    public interface IOptionalAiProvider
    {
        string ProviderName { get; }
        bool IsConfigured { get; }
        string Model { get; }
        Task<string?> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken = default);
    }
}
