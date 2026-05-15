using CivicOps.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CivicOps.Services
{
    public interface IDataService
    {
        Task<List<Incident>> GetAllIncidentsAsync();
        Task<Incident?> GetIncidentByIdAsync(string id);
        Task<Incident?> GetIncidentByReferenceAsync(string reference);
        Task<List<Incident>> GetIncidentsByDepartmentAsync(Department department);
        Task<string> SaveIncidentAsync(Incident incident);
        Task UpdateIncidentAsync(Incident incident);
        
        Task<List<Alert>> GetAllAlertsAsync();
        Task<List<Alert>> GetAlertsByAreaAsync(string? suburb = null, string? ward = null);
        Task<string> SaveAlertAsync(Alert alert);
        
        Task<string> GenerateReferenceNumberAsync();
        Task InitializeAsync();
    }
}
