using CivicOps.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace CivicOps.Services
{
    public class IncidentIntakeService : IIncidentIntakeService
    {
        private readonly IDataService _dataService;
        private readonly IGeminiService _geminiService;
        private readonly IClassificationService _classificationService;

        private static readonly Dictionary<string, AreaProfile> AreaAliases = new(StringComparer.OrdinalIgnoreCase)
        {
            ["chatworth"] = new("Chatsworth", "Ward 73", "High", "alias corrected from common misspelling"),
            ["chatswoth"] = new("Chatsworth", "Ward 73", "High", "alias corrected from common misspelling"),
            ["chastworth"] = new("Chatsworth", "Ward 73", "High", "alias corrected from common misspelling"),
            ["chatsworth"] = new("Chatsworth", "Ward 73", "High", "synthetic demo ward estimate"),
            ["pheonix"] = new("Phoenix", "Ward 52", "High", "alias corrected from common misspelling"),
            ["phoenix"] = new("Phoenix", "Ward 52", "High", "synthetic demo ward estimate"),
            ["durbn cbd"] = new("Durban CBD", "Ward 26", "High", "alias corrected from common misspelling"),
            ["dbn town"] = new("Durban CBD", "Ward 26", "High", "alias corrected from common shorthand"),
            ["durban cbd"] = new("Durban CBD", "Ward 26", "High", "synthetic demo ward estimate"),
            ["umlaz"] = new("Umlazi", "Ward 80", "High", "alias corrected from common misspelling"),
            ["umlazi"] = new("Umlazi", "Ward 80", "High", "synthetic demo ward estimate"),
            ["kwamashu"] = new("KwaMashu", "Ward 55", "High", "synthetic demo ward estimate"),
            ["kwa mashu"] = new("KwaMashu", "Ward 55", "High", "alias corrected from spacing variant"),
            ["pinetowm"] = new("Pinetown", "Ward 23", "High", "alias corrected from common misspelling"),
            ["pinetown"] = new("Pinetown", "Ward 23", "High", "synthetic demo ward estimate"),
            ["amanzi mtoti"] = new("Amanzimtoti", "Ward 97", "High", "alias corrected from spacing variant"),
            ["amanzimtoti"] = new("Amanzimtoti", "Ward 97", "High", "synthetic demo ward estimate"),
            ["ethekweni"] = new("eThekwini", "Needs ward confirmation", "Low", "municipality spelling corrected; suburb still needs confirmation"),
            ["ethekwini"] = new("eThekwini", "Needs ward confirmation", "Low", "municipality supplied; suburb still needs confirmation"),
            ["westville"] = new("Westville", "Ward 24", "Medium", "synthetic demo ward estimate"),
            ["newlands"] = new("Newlands", "Ward 31", "Medium", "synthetic demo ward estimate"),
            ["inanda"] = new("Inanda", "Ward 57", "Medium", "synthetic demo ward estimate"),
            ["isipingo"] = new("Isipingo", "Ward 68", "Medium", "synthetic demo ward estimate"),
            ["bluff"] = new("Bluff", "Ward 66", "Medium", "synthetic demo ward estimate"),
            ["berea"] = new("Berea", "Ward 31", "Medium", "synthetic demo ward estimate"),
            ["glenwood"] = new("Glenwood", "Ward 33", "Medium", "synthetic demo ward estimate"),
            ["hillcrest"] = new("Hillcrest", "Ward 8", "Medium", "synthetic demo ward estimate"),
            ["reservoir hills"] = new("Reservoir Hills", "Ward 23", "Medium", "synthetic demo ward estimate"),
            ["tongaat"] = new("Tongaat", "Ward 61", "Medium", "synthetic demo ward estimate"),
            ["verulam"] = new("Verulam", "Ward 58", "Medium", "synthetic demo ward estimate"),
            ["cato manor"] = new("Cato Manor", "Ward 28", "Medium", "synthetic demo ward estimate"),
            ["mobeni"] = new("Mobeni", "Ward 75", "Medium", "synthetic demo ward estimate"),
            ["merebank"] = new("Merebank", "Ward 70", "Medium", "synthetic demo ward estimate"),
            ["montclair"] = new("Montclair", "Ward 64", "Medium", "synthetic demo ward estimate"),
            ["queensburgh"] = new("Queensburgh", "Ward 65", "Medium", "synthetic demo ward estimate"),
            ["malvern"] = new("Malvern", "Ward 18", "Medium", "synthetic demo ward estimate"),
            ["umhlanga"] = new("Umhlanga", "Ward 35", "Medium", "synthetic demo ward estimate"),
            ["la lucia"] = new("La Lucia", "Ward 36", "Medium", "synthetic demo ward estimate")
        };

        public IncidentIntakeService(IDataService dataService, IGeminiService geminiService, IClassificationService classificationService)
        {
            _dataService = dataService;
            _geminiService = geminiService;
            _classificationService = classificationService;
        }

        public async Task<IncidentIntakeResult> ProcessAsync(IncidentIntakeRequest request)
        {
            var rawDescription = request.Description?.Trim() ?? string.Empty;
            var validation = Validate(rawDescription);
            var area = NormalizeArea(request.Suburb, rawDescription, request.LocationNotes);
            var cleanedDescription = CleanDescription(rawDescription, area);
            var classification = await _geminiService.ClassifyWithGeminiAsync(cleanedDescription, request.Category, $"{request.SourceChannel}-intake");
            if (classification.Department == Department.WaterAndSanitation && LooksLikeStormwater(cleanedDescription))
            {
                classification.Department = Department.RoadsAndStormwater;
                classification.Category = "Stormwater";
                classification.Summary = $"Stormwater: {TrimSentence(cleanedDescription)}";
            }

            var ward = !string.IsNullOrWhiteSpace(request.Ward) ? request.Ward.Trim() : area.Ward;
            var wardConfidence = !string.IsNullOrWhiteSpace(request.Ward) ? "Resident supplied" : area.WardConfidence;
            if (string.IsNullOrWhiteSpace(ward))
            {
                ward = "Needs ward confirmation";
                wardConfidence = "Low";
            }

            var incident = new Incident
            {
                SourceChannel = request.SourceChannel,
                Description = rawDescription,
                RawDescription = rawDescription,
                CleanedDescription = cleanedDescription,
                AISummary = classification.Summary,
                Category = classification.Category,
                AssignedDepartment = classification.Department,
                Suburb = area.NormalizedArea,
                OriginalArea = area.OriginalArea,
                NormalizedArea = area.NormalizedArea,
                Municipality = "eThekwini",
                Ward = ward,
                WardConfidence = wardConfidence,
                Status = IncidentStatus.Triaged,
                Priority = classification.Priority,
                ContactName = request.ContactName,
                ContactPhone = request.ContactPhone,
                ContactEmail = request.ContactEmail,
                LocationNotes = request.LocationNotes,
                MediaMetadata = request.MediaMetadata,
                AudioMetadata = request.AudioMetadata,
                IsGeminiProcessed = classification.IsGeminiProcessed,
                ClassificationMethod = classification.Method,
                EnrichmentSource = classification.IsGeminiProcessed ? classification.Method : "Local deterministic fallback",
                EnrichmentNotes = area.Notes,
                RoutingConfidence = BuildRoutingConfidence(classification.Method, wardConfidence, validation.IsValid),
                EmergencyReferralRecommended = IsEmergency(rawDescription)
            };

            foreach (var item in request.ConnectorMetadata)
            {
                incident.ConnectorMetadata[item.Key] = item.Value;
            }

            var routingReason = BuildRoutingReason(incident);
            var alertRecommendation = BuildAlertRecommendation(incident);
            var citizenResponse = BuildCitizenResponse(incident, validation.Message);
            var departmentBrief = BuildDepartmentBrief(incident, validation.Message);

            incident.RoutingReason = routingReason;
            incident.AlertRecommendation = alertRecommendation;
            incident.CitizenResponse = citizenResponse;
            incident.DepartmentBrief = departmentBrief;
            incident.ConnectorMetadata["validation"] = validation.Message;
            incident.ConnectorMetadata["routing_reason"] = routingReason;
            incident.ConnectorMetadata["alert_recommendation"] = alertRecommendation;
            incident.ConnectorMetadata["department_brief"] = departmentBrief;
            incident.ConnectorMetadata["original_area"] = incident.OriginalArea;
            incident.ConnectorMetadata["normalized_area"] = incident.NormalizedArea;
            incident.ConnectorMetadata["ward_confidence"] = incident.WardConfidence;

            incident.InternalNotes.Add(new IncidentNote
            {
                Author = "CivicOps Gemini/fallback intake pipeline",
                Content = $"Validation: {validation.Message}. Raw: '{rawDescription}'. Cleaned: '{cleanedDescription}'. Area normalized from '{incident.OriginalArea}' to '{incident.NormalizedArea}'. Ward: {incident.Ward} ({incident.WardConfidence}). Routed to {incident.AssignedDepartment.GetDisplayName()} as {incident.Priority}. {routingReason} Alert recommendation: {alertRecommendation} Source: {incident.EnrichmentSource}. Notes: {incident.EnrichmentNotes}",
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
            incident.CitizenResponse = citizenResponse;
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

        private static AreaProfile NormalizeArea(string? submittedArea, string description, string? locationNotes)
        {
            var original = string.IsNullOrWhiteSpace(submittedArea) ? ExtractKnownArea(description, locationNotes) : submittedArea.Trim();
            if (string.IsNullOrWhiteSpace(original))
            {
                return new AreaProfile("Needs area confirmation", "Needs ward confirmation", "Low", "No area supplied; staff must confirm suburb/ward") { OriginalArea = "Not supplied" };
            }
            var key = Regex.Replace(original.Trim().ToLowerInvariant(), @"\s+", " ");
            if (AreaAliases.TryGetValue(key, out var exact)) return exact with { OriginalArea = original.Trim() };
            foreach (var kvp in AreaAliases)
            {
                if (description.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase) || (locationNotes?.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase) ?? false))
                {
                    return kvp.Value with { OriginalArea = original.Trim() };
                }
            }
            return new AreaProfile(ToTitle(original), "Needs ward confirmation", "Low", "Area kept as supplied; ward requires staff confirmation") { OriginalArea = original.Trim() };
        }

        private static string ExtractKnownArea(string description, string? locationNotes)
        {
            var haystack = $"{description} {locationNotes}";
            return AreaAliases.Keys.OrderByDescending(k => k.Length).FirstOrDefault(k => haystack.Contains(k, StringComparison.OrdinalIgnoreCase)) ?? string.Empty;
        }

        private static string CleanDescription(string description, AreaProfile area)
        {
            var cleaned = Regex.Replace(description.Trim(), @"\s+", " ");
            if (!string.Equals(area.OriginalArea, area.NormalizedArea, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(area.OriginalArea) && area.OriginalArea != "Not supplied")
            {
                cleaned = Regex.Replace(cleaned, Regex.Escape(area.OriginalArea), area.NormalizedArea, RegexOptions.IgnoreCase);
            }
            cleaned = Regex.Replace(cleaned, "blocked drain", "blocked stormwater drain", RegexOptions.IgnoreCase);
            return cleaned;
        }

        private static bool LooksLikeStormwater(string description)
        {
            var lower = description.ToLowerInvariant();
            return lower.Contains("drain") || lower.Contains("stormwater") || lower.Contains("water pooling") || lower.Contains("flooding") || lower.Contains("pothole") || lower.Contains("traffic light");
        }

        private static (bool IsValid, string Message) Validate(string? description)
        {
            if (string.IsNullOrWhiteSpace(description)) return (false, "Insufficient information: description is required.");
            var trimmed = description.Trim();
            if (trimmed.Length < 10) return (false, "Insufficient information: please include the issue and location.");
            var lower = trimmed.ToLowerInvariant();
            if (new[] { "test spam", "asdf", "qwerty" }.Any(lower.Contains)) return (false, "Possible spam or test content; human review recommended.");
            if (IsEmergency(trimmed)) return (true, "Emergency-language detected: route civic ticket and advise citizen to contact emergency services directly.");
            return (true, "Valid civic report with enough information for routing.");
        }

        private static bool IsEmergency(string description) => new[] { "life threatening", "trapped", "medical emergency", "active fire", "danger to life" }.Any(description.ToLowerInvariant().Contains);

        private static string BuildRoutingReason(Incident incident)
        {
            return $"Routed to {incident.AssignedDepartment.GetDisplayName()} because the report describes {incident.Category.ToLowerInvariant()} impacts in {incident.NormalizedArea}. Routing confidence: {incident.RoutingConfidence}.";
        }

        private static string BuildAlertRecommendation(Incident incident)
        {
            if (incident.Priority == IncidentPriority.Urgent || incident.Status == IncidentStatus.Escalated) return "Review for Area Alerts and dispatcher escalation; publish only after staff confirmation.";
            if (incident.AssignedDepartment == Department.RoadsAndStormwater && incident.Category.Contains("Stormwater", StringComparison.OrdinalIgnoreCase)) return "Monitor repeat reports and rainfall risk before issuing an Area Alert.";
            return "No automatic public alert; keep ticket-level public updates active.";
        }

        private static string BuildCitizenResponse(Incident incident, string validation)
        {
            var wardText = incident.Ward.Contains("Needs", StringComparison.OrdinalIgnoreCase) ? "Needs ward confirmation" : $"Ward estimate: {incident.Ward}";
            var areaText = string.Equals(incident.OriginalArea, incident.NormalizedArea, StringComparison.OrdinalIgnoreCase) ? $"Area recorded as {incident.NormalizedArea}." : $"Area normalized from '{incident.OriginalArea}' to '{incident.NormalizedArea}'.";
            var emergency = incident.EmergencyReferralRecommended ? " If anyone is in immediate danger, contact emergency services directly; CivicOps does not replace emergency services." : string.Empty;
            return $"Reference {incident.ReferenceNumber}: {areaText} {wardText}. Routed to {incident.AssignedDepartment.GetDisplayName()} because {incident.RoutingReason}{emergency} Source: {incident.EnrichmentSource}.";
        }

        private static string BuildDepartmentBrief(Incident incident, string validation)
        {
            return $"{incident.Priority} {incident.Category} ticket for {incident.NormalizedArea} ({incident.Ward}, confidence {incident.WardConfidence}). Raw citizen text: {incident.RawDescription}. Cleaned text: {incident.CleanedDescription}. {validation}";
        }

        private static string BuildRoutingConfidence(string method, string wardConfidence, bool valid)
        {
            if (!valid) return "Low - validation warning";
            if (wardConfidence.Equals("High", StringComparison.OrdinalIgnoreCase) || wardConfidence.Equals("Resident supplied", StringComparison.OrdinalIgnoreCase)) return method.Contains("fallback", StringComparison.OrdinalIgnoreCase) ? "High deterministic" : "High AI-assisted";
            return "Medium - ward needs staff confirmation";
        }

        private static string TrimSentence(string text) => text.Length > 110 ? text[..107] + "..." : text;
        private static string ToTitle(string text) => System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(text.Trim().ToLowerInvariant());

        private record AreaProfile(string NormalizedArea, string Ward, string WardConfidence, string Notes)
        {
            public string OriginalArea { get; init; } = NormalizedArea;
        }
    }
}
