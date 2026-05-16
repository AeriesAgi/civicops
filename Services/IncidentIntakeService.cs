using CivicOps.Models;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace CivicOps.Services
{
    public class IncidentIntakeService : IIncidentIntakeService
    {
        private readonly IDataService _dataService;
        private readonly IGeminiService _geminiService;
        private readonly IClassificationService _classificationService;

        public IncidentIntakeService(
            IDataService dataService,
            IGeminiService geminiService,
            IClassificationService classificationService)
        {
            _dataService = dataService;
            _geminiService = geminiService;
            _classificationService = classificationService;
        }

        public async Task<IncidentIntakeResult> ProcessAsync(IncidentIntakeRequest request)
        {
            var validation = Validate(request.Description);
            // Event-triggered AI path: this method is called only from report submission,
            // inbound connector processing, voice transcript submission, or explicit agent actions.
            // GeminiService itself decides whether to make one live call or use local fallback.
            var classification = await _geminiService.ClassifyWithGeminiAsync(request.Description, request.Category, $"{request.SourceChannel}-intake");

            var incident = new Incident
            {
                SourceChannel = request.SourceChannel,
                Description = request.Description.Trim(),
                AISummary = classification.Summary,
                Category = classification.Category,
                AssignedDepartment = classification.Department,
                Suburb = string.IsNullOrWhiteSpace(request.Suburb) ? "Unknown" : request.Suburb.Trim(),
                Ward = string.IsNullOrWhiteSpace(request.Ward) ? "Unknown" : request.Ward.Trim(),
                Status = IncidentStatus.Triaged,
                Priority = classification.Priority,
                ContactName = request.ContactName,
                ContactPhone = request.ContactPhone,
                ContactEmail = request.ContactEmail,
                LocationNotes = request.LocationNotes,
                MediaMetadata = request.MediaMetadata,
                AudioMetadata = request.AudioMetadata,
                IsGeminiProcessed = classification.IsGeminiProcessed,
                ClassificationMethod = classification.Method
            };

            foreach (var item in request.ConnectorMetadata)
            {
                incident.ConnectorMetadata[item.Key] = item.Value;
            }

            var routingReason = BuildRoutingReason(incident);
            var alertRecommendation = BuildAlertRecommendation(incident);
            var citizenResponse = BuildCitizenResponse(incident, validation.Message);

            incident.ConnectorMetadata["validation"] = validation.Message;
            incident.ConnectorMetadata["routing_reason"] = routingReason;
            incident.ConnectorMetadata["alert_recommendation"] = alertRecommendation;

            incident.InternalNotes.Add(new IncidentNote
            {
                Author = "CivicOps Intake Pipeline",
                Content = $"Validation: {validation.Message}. Routed to {incident.AssignedDepartment.GetDisplayName()} as {incident.Priority}. {routingReason} Alert recommendation: {alertRecommendation}",
                IsPublic = false
            });

            incident.PublicUpdates.Add(new PublicUpdate
            {
                Content = citizenResponse,
                UpdatedBy = request.CreatedBy,
                RelatedStatus = incident.Status
            });

            await _dataService.SaveIncidentAsync(incident);
            citizenResponse = BuildCitizenResponse(incident, validation.Message);
            incident.PublicUpdates[0].Content = citizenResponse;
            await _dataService.UpdateIncidentAsync(incident);

            return new IncidentIntakeResult
            {
                IsValid = validation.IsValid,
                Validation = validation.Message,
                Incident = incident,
                CitizenResponse = citizenResponse,
                RoutingReason = routingReason,
                AlertRecommendation = alertRecommendation
            };
        }

        private static (bool IsValid, string Message) Validate(string? description)
        {
            if (string.IsNullOrWhiteSpace(description))
            {
                return (false, "Insufficient information: description is required.");
            }

            var trimmed = description.Trim();
            if (trimmed.Length < 10)
            {
                return (false, "Insufficient information: please include the issue and location.");
            }

            var lower = trimmed.ToLowerInvariant();
            if (new[] { "test spam", "asdf", "qwerty" }.Any(lower.Contains))
            {
                return (false, "Possible spam or test content; human review recommended.");
            }

            if (new[] { "life threatening", "trapped", "medical emergency", "active fire" }.Any(lower.Contains))
            {
                return (true, "Emergency-language detected: route civic ticket and advise citizen to contact emergency services directly.");
            }

            return (true, "Valid civic report with enough information for routing.");
        }

        private static string BuildRoutingReason(Incident incident)
        {
            return $"{incident.Category} maps to {incident.AssignedDepartment.GetDisplayName()} with {incident.Priority} priority based on reported impact and keywords.";
        }

        private static string BuildAlertRecommendation(Incident incident)
        {
            if (incident.Priority == IncidentPriority.Urgent)
            {
                return "Review for public alert or dispatcher escalation.";
            }

            if (incident.Category.Contains("Water", StringComparison.OrdinalIgnoreCase) ||
                incident.Category.Contains("Electricity", StringComparison.OrdinalIgnoreCase) ||
                incident.Category.Contains("Flood", StringComparison.OrdinalIgnoreCase))
            {
                return "Monitor for duplicate area reports before issuing an alert.";
            }

            return "No immediate public alert recommended.";
        }

        private static string BuildCitizenResponse(Incident incident, string validation)
        {
            return $"Your report has been received. Reference: {incident.ReferenceNumber}. Validation: {validation} Routed to {incident.AssignedDepartment.GetDisplayName()} as {incident.Priority} priority.";
        }
    }
}
