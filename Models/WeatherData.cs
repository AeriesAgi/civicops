using System;

namespace CivicOps.Models
{
    public class WeatherData
    {
        public string AreaName { get; set; } = string.Empty;
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double Temperature { get; set; }
        public double PrecipitationProbability { get; set; }
        public double Precipitation { get; set; }
        public double WindSpeed { get; set; }
        public string WeatherCondition { get; set; } = string.Empty;
        public int WeatherCode { get; set; }
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public string RiskLevel { get; set; } = "Normal";
        public string RiskDescription { get; set; } = string.Empty;
    }

    public class AreaCoordinate
    {
        public string Name { get; set; } = string.Empty;
        public string Municipality { get; set; } = string.Empty;
        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }
}
