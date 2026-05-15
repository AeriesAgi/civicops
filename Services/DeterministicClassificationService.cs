using CivicOps.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CivicOps.Services
{
    public class DeterministicClassificationService : IClassificationService
    {
        private readonly Dictionary<string, (Department Department, string Category)> _keywordMappings;

        public DeterministicClassificationService()
        {
            _keywordMappings = new Dictionary<string, (Department, string)>(StringComparer.OrdinalIgnoreCase)
            {
                // Water & Sanitation
                { "water", (Department.WaterAndSanitation, "Water Infrastructure") },
                { "pipe", (Department.WaterAndSanitation, "Water Infrastructure") },
                { "burst", (Department.WaterAndSanitation, "Water Infrastructure") },
                { "leak", (Department.WaterAndSanitation, "Water Infrastructure") },
                { "sewage", (Department.WaterAndSanitation, "Sanitation") },
                { "sewer", (Department.WaterAndSanitation, "Sanitation") },
                { "toilet", (Department.WaterAndSanitation, "Sanitation") },
                { "sanitation", (Department.WaterAndSanitation, "Sanitation") },
                
                // Electricity
                { "electricity", (Department.Electricity, "Electricity") },
                { "power", (Department.Electricity, "Electricity") },
                { "outage", (Department.Electricity, "Electricity") },
                { "blackout", (Department.Electricity, "Electricity") },
                { "transformer", (Department.Electricity, "Electricity") },
                { "streetlight", (Department.Electricity, "Street Lighting") },
                
                // Roads & Stormwater
                { "road", (Department.RoadsAndStormwater, "Road Maintenance") },
                { "pothole", (Department.RoadsAndStormwater, "Road Maintenance") },
                { "street", (Department.RoadsAndStormwater, "Road Maintenance") },
                { "pavement", (Department.RoadsAndStormwater, "Road Maintenance") },
                { "drain", (Department.RoadsAndStormwater, "Stormwater") },
                { "stormwater", (Department.RoadsAndStormwater, "Stormwater") },
                { "flooding", (Department.RoadsAndStormwater, "Stormwater") },
                { "flood", (Department.RoadsAndStormwater, "Stormwater") },
                
                // Waste Management
                { "waste", (Department.WasteManagement, "Waste Collection") },
                { "refuse", (Department.WasteManagement, "Waste Collection") },
                { "garbage", (Department.WasteManagement, "Waste Collection") },
                { "rubbish", (Department.WasteManagement, "Waste Collection") },
                { "bin", (Department.WasteManagement, "Waste Collection") },
                { "dumping", (Department.WasteManagement, "Illegal Dumping") },
                { "litter", (Department.WasteManagement, "Illegal Dumping") },
                
                // Parks & Public Spaces
                { "park", (Department.ParksAndPublicSpaces, "Parks") },
                { "playground", (Department.ParksAndPublicSpaces, "Parks") },
                { "garden", (Department.ParksAndPublicSpaces, "Parks") },
                { "grass", (Department.ParksAndPublicSpaces, "Parks") },
                { "tree", (Department.ParksAndPublicSpaces, "Parks") },
                
                // Environmental Health
                { "pollution", (Department.EnvironmentalHealth, "Environmental Hazard") },
                { "hazard", (Department.EnvironmentalHealth, "Environmental Hazard") },
                { "contamination", (Department.EnvironmentalHealth, "Environmental Hazard") },
                { "smell", (Department.EnvironmentalHealth, "Environmental Hazard") },
                
                // Fire & Rescue
                { "fire", (Department.FireAndRescue, "Fire Safety") },
                { "smoke", (Department.FireAndRescue, "Fire Safety") },
                { "burning", (Department.FireAndRescue, "Fire Safety") },
                
                // Disaster Management
                { "disaster", (Department.DisasterManagement, "Disaster") },
                { "emergency", (Department.DisasterManagement, "Disaster") },
                { "evacuation", (Department.DisasterManagement, "Disaster") },
                
                // Public Safety
                { "safety", (Department.MetroPolicePublicSafety, "Public Safety") },
                { "crime", (Department.MetroPolicePublicSafety, "Public Safety") },
                { "suspicious", (Department.MetroPolicePublicSafety, "Public Safety") },
                { "patrol", (Department.MetroPolicePublicSafety, "Public Safety") },
                
                // Housing
                { "housing", (Department.HousingInformalSettlements, "Housing") },
                { "shack", (Department.HousingInformalSettlements, "Informal Settlement") },
                { "settlement", (Department.HousingInformalSettlements, "Informal Settlement") },
            };
        }

        public Task<ClassificationResult> ClassifyIncidentAsync(string description, string? category = null)
        {
            var result = new ClassificationResult
            {
                Method = "Deterministic",
                IsGeminiProcessed = false
            };

            // Determine priority based on keywords
            result.Priority = DeterminePriority(description);

            // Find matching department and category
            var match = FindBestMatch(description);
            result.Department = match.Department;
            result.Category = match.Category;

            // Generate summary
            result.Summary = GenerateSummary(description, result.Category);

            return Task.FromResult(result);
        }

        private (Department Department, string Category) FindBestMatch(string description)
        {
            var words = description.ToLower().Split(new[] { ' ', ',', '.', '!', '?' }, StringSplitOptions.RemoveEmptyEntries);
            
            foreach (var word in words)
            {
                if (_keywordMappings.TryGetValue(word, out var mapping))
                {
                    return mapping;
                }
            }

            // Default fallback
            return (Department.WardCouncillorWardCommittee, "General Inquiry");
        }

        private IncidentPriority DeterminePriority(string description)
        {
            var lowerDesc = description.ToLower();
            
            // Urgent keywords
            if (lowerDesc.Contains("emergency") || lowerDesc.Contains("urgent") || 
                lowerDesc.Contains("danger") || lowerDesc.Contains("fire") ||
                lowerDesc.Contains("flood") || lowerDesc.Contains("burst"))
            {
                return IncidentPriority.Urgent;
            }

            // High priority keywords
            if (lowerDesc.Contains("leak") || lowerDesc.Contains("sewage") ||
                lowerDesc.Contains("power") || lowerDesc.Contains("outage") ||
                lowerDesc.Contains("safety") || lowerDesc.Contains("crime"))
            {
                return IncidentPriority.High;
            }

            // Low priority keywords
            if (lowerDesc.Contains("grass") || lowerDesc.Contains("litter") ||
                lowerDesc.Contains("paint"))
            {
                return IncidentPriority.Low;
            }

            return IncidentPriority.Medium;
        }

        private string GenerateSummary(string description, string category)
        {
            // Simple summarization - take first sentence or truncate
            var firstSentence = description.Split('.', '!', '?').FirstOrDefault()?.Trim() ?? description;
            
            if (firstSentence.Length > 100)
            {
                firstSentence = firstSentence.Substring(0, 97) + "...";
            }

            return $"{category}: {firstSentence}";
        }
    }
}
