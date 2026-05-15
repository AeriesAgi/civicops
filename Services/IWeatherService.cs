using CivicOps.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CivicOps.Services
{
    public interface IWeatherService
    {
        Task<WeatherData?> GetWeatherForAreaAsync(string areaName);
        Task<List<AreaCoordinate>> GetAvailableAreasAsync();
        Task<List<WeatherData>> GetWeatherForAllAreasAsync();
        string GetWeatherConditionDescription(int weatherCode);
        string AssessRiskLevel(WeatherData weather);
    }
}
