using CivicOps.Models;
using System.Threading.Tasks;

namespace CivicOps.Services
{
    public interface IDemoAuthService
    {
        Task<DemoAuthSession?> LoginAsync(string email, string password);
        Task LogoutAsync(string sessionId);
        Task<DemoAuthSession?> GetSessionAsync(string sessionId);
        Task<bool> IsAuthorizedAsync(string sessionId, UserRole requiredRole);
        Task<DemoUser?> GetUserByEmailAsync(string email);
        Task InitializeAsync();
    }
}
