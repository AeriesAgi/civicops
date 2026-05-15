using CivicOps.Models;
using System.Threading.Tasks;

namespace CivicOps.Services
{
    public interface IResidentAuthService
    {
        Task InitializeAsync();
        Task<ResidentUser?> AuthenticateAsync(string email, string password);
        Task<ResidentUser?> GetUserByEmailAsync(string email);
        Task<ResidentUser?> GetUserByIdAsync(string userId);
        Task<ResidentUser> CreateUserAsync(string email, string password, string fullName, string? phoneNumber = null);
        Task UpdateUserAsync(ResidentUser user);
        Task AddReportReferenceAsync(string userId, string referenceNumber);
        Task AddFollowedSuburbAsync(string userId, string suburb);
        Task RemoveFollowedSuburbAsync(string userId, string suburb);
        Task AddFollowedWardAsync(string userId, string ward);
        Task RemoveFollowedWardAsync(string userId, string ward);
    }
}
