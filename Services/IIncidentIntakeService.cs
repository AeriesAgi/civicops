using CivicOps.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CivicOps.Services
{
    public class IncidentIntakeRequest
    {
        public SourceChannel SourceChannel { get; set; } = SourceChannel.Web;
        public string Description { get; set; } = string.Empty;
        public string? Category { get; set; }
        public string? Suburb { get; set; }
        public string? Ward { get; set; }
        public string? ContactName { get; set; }
        public string? ContactPhone { get; set; }
        public string? ContactEmail { get; set; }
        public string? LocationNotes { get; set; }
        public string? MediaMetadata { get; set; }
        public string? AudioMetadata { get; set; }
        public string CreatedBy { get; set; } = "System";
        public Dictionary<string, string> ConnectorMetadata { get; set; } = new();
    }

    public class IncidentIntakeResult
    {
        public bool IsValid { get; set; }
        public string Validation { get; set; } = string.Empty;
        public Incident Incident { get; set; } = new();
        public string CitizenResponse { get; set; } = string.Empty;
        public string RoutingReason { get; set; } = string.Empty;
        public string AlertRecommendation { get; set; } = string.Empty;
    }

    public interface IIncidentIntakeService
    {
        Task<IncidentIntakeResult> ProcessAsync(IncidentIntakeRequest request);
    }
}
