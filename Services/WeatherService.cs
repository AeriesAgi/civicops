using CivicOps.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace CivicOps.Services
{
    public class WeatherService : IWeatherService
    {
        private readonly HttpClient _httpClient;
        private readonly Dictionary<string, WeatherData> _cache = new();
        private readonly object _cacheLock = new();
        private DateTime _lastCacheUpdate = DateTime.MinValue;
        private readonly TimeSpan _cacheExpiry = TimeSpan.FromMinutes(30);

        private readonly List<AreaCoordinate> _areas = new()
        {
            // Durban / eThekwini areas
            new AreaCoordinate { Name = "Chatsworth", Municipality = "eThekwini", Latitude = -29.9167, Longitude = 30.8833 },
            new AreaCoordinate { Name = "Umlazi", Municipality = "eThekwini", Latitude = -29.9667, Longitude = 30.8833 },
            new AreaCoordinate { Name = "Phoenix", Municipality = "eThekwini", Latitude = -29.7000, Longitude = 31.0000 },
            new AreaCoordinate { Name = "Durban CBD", Municipality = "eThekwini", Latitude = -29.8587, Longitude = 31.0218 },
            new AreaCoordinate { Name = "Pinetown", Municipality = "eThekwini", Latitude = -29.8167, Longitude = 30.8667 },
            new AreaCoordinate { Name = "Bluff", Municipality = "eThekwini", Latitude = -29.9167, Longitude = 31.0000 },
            new AreaCoordinate { Name = "KwaMashu", Municipality = "eThekwini", Latitude = -29.7333, Longitude = 30.9833 },
            new AreaCoordinate { Name = "Isipingo", Municipality = "eThekwini", Latitude = -30.0000, Longitude = 30.9333 },
            new AreaCoordinate { Name = "Hillcrest", Municipality = "eThekwini", Latitude = -29.7833, Longitude = 30.7667 },
            
            // Cape Town / Western Cape areas
            new AreaCoordinate { Name = "Cape Town CBD", Municipality = "City of Cape Town", Latitude = -33.9249, Longitude = 18.4241 },
            new AreaCoordinate { Name = "Khayelitsha", Municipality = "City of Cape Town", Latitude = -34.0500, Longitude = 18.6667 },
            new AreaCoordinate { Name = "Mitchells Plain", Municipality = "City of Cape Town", Latitude = -34.0500, Longitude = 18.6167 },
            new AreaCoordinate { Name = "Bellville", Municipality = "City of Cape Town", Latitude = -33.8833, Longitude = 18.6333 },
            new AreaCoordinate { Name = "Strand", Municipality = "City of Cape Town", Latitude = -34.1167, Longitude = 18.8167 },
            new AreaCoordinate { Name = "Somerset West", Municipality = "City of Cape Town", Latitude = -34.0833, Longitude = 18.8500 },
            new AreaCoordinate { Name = "Table View", Municipality = "City of Cape Town", Latitude = -33.8167, Longitude = 18.5000 },
            new AreaCoordinate { Name = "Hout Bay", Municipality = "City of Cape Town", Latitude = -34.0333, Longitude = 18.3500 }
        };

        public WeatherService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public Task<List<AreaCoordinate>> GetAvailableAreasAsync()
        {
            return Task.FromResult(_areas);
        }

        public async Task<WeatherData?> GetWeatherForAreaAsync(string areaName)
        {
            // Check cache first
            lock (_cacheLock)
            {
                if (_cache.TryGetValue(areaName, out var cachedData) && 
                    DateTime.UtcNow - _lastCacheUpdate < _cacheExpiry)
                {
                    return cachedData;
                }
            }

            var area = _areas.FirstOrDefault(a => a.Name.Equals(areaName, StringComparison.OrdinalIgnoreCase));
            if (area == null)
            {
                return null;
            }

            try
            {
                // Open-Meteo API - free, no key required
                var url = $"https://api.open-meteo.com/v1/forecast?latitude={area.Latitude}&longitude={area.Longitude}&current=temperature_2m,precipitation,weather_code,wind_speed_10m&hourly=precipitation_probability&timezone=auto";
                
                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                var json = await response.Content.ReadAsStringAsync();
                var data = JsonSerializer.Deserialize<JsonElement>(json);

                var current = data.GetProperty("current");
                var hourly = data.GetProperty("hourly");
                
                var weatherData = new WeatherData
                {
                    AreaName = area.Name,
                    Latitude = area.Latitude,
                    Longitude = area.Longitude,
                    Temperature = current.GetProperty("temperature_2m").GetDouble(),
                    Precipitation = current.GetProperty("precipitation").GetDouble(),
                    WindSpeed = current.GetProperty("wind_speed_10m").GetDouble(),
                    WeatherCode = current.GetProperty("weather_code").GetInt32(),
                    UpdatedAt = DateTime.UtcNow
                };

                // Get precipitation probability from hourly data (first hour)
                if (hourly.TryGetProperty("precipitation_probability", out var precipProb))
                {
                    var probArray = precipProb.EnumerateArray().ToList();
                    if (probArray.Any())
                    {
                        weatherData.PrecipitationProbability = probArray[0].GetDouble();
                    }
                }

                weatherData.WeatherCondition = GetWeatherConditionDescription(weatherData.WeatherCode);
                weatherData.RiskLevel = AssessRiskLevel(weatherData);
                weatherData.RiskDescription = GetRiskDescription(weatherData);

                // Update cache
                lock (_cacheLock)
                {
                    _cache[areaName] = weatherData;
                    _lastCacheUpdate = DateTime.UtcNow;
                }

                return weatherData;
            }
            catch (Exception)
            {
                // Return null on error - service will handle gracefully
                return null;
            }
        }

        public async Task<List<WeatherData>> GetWeatherForAllAreasAsync()
        {
            var tasks = _areas.Select(a => GetWeatherForAreaAsync(a.Name));
            var results = await Task.WhenAll(tasks);
            return results.Where(r => r != null).ToList()!;
        }

        public string GetWeatherConditionDescription(int weatherCode)
        {
            // WMO Weather interpretation codes
            return weatherCode switch
            {
                0 => "Clear sky",
                1 => "Mainly clear",
                2 => "Partly cloudy",
                3 => "Overcast",
                45 or 48 => "Fog",
                51 or 53 or 55 => "Drizzle",
                56 or 57 => "Freezing drizzle",
                61 or 63 or 65 => "Rain",
                66 or 67 => "Freezing rain",
                71 or 73 or 75 => "Snow",
                77 => "Snow grains",
                80 or 81 or 82 => "Rain showers",
                85 or 86 => "Snow showers",
                95 => "Thunderstorm",
                96 or 99 => "Thunderstorm with hail",
                _ => "Unknown"
            };
        }

        public string AssessRiskLevel(WeatherData weather)
        {
            // Heavy rain risk
            if (weather.Precipitation > 10 || weather.PrecipitationProbability > 70)
            {
                return "High";
            }

            // High wind risk
            if (weather.WindSpeed > 50)
            {
                return "High";
            }

            // Thunderstorm risk
            if (weather.WeatherCode >= 95)
            {
                return "High";
            }

            // Moderate rain or wind
            if (weather.Precipitation > 5 || weather.WindSpeed > 30 || weather.PrecipitationProbability > 50)
            {
                return "Moderate";
            }

            // Extreme heat (for South African context)
            if (weather.Temperature > 35)
            {
                return "Moderate";
            }

            return "Normal";
        }

        private string GetRiskDescription(WeatherData weather)
        {
            var risks = new List<string>();

            if (weather.Precipitation > 10)
            {
                risks.Add("Heavy rain - flood/stormwater risk");
            }
            else if (weather.Precipitation > 5)
            {
                risks.Add("Moderate rain - monitor stormwater drains");
            }

            if (weather.WindSpeed > 50)
            {
                risks.Add("High winds - infrastructure/public safety caution");
            }
            else if (weather.WindSpeed > 30)
            {
                risks.Add("Moderate winds - secure loose objects");
            }

            if (weather.WeatherCode >= 95)
            {
                risks.Add("Thunderstorm - stay indoors, avoid open areas");
            }

            if (weather.Temperature > 35)
            {
                risks.Add("Extreme heat - health advisory, stay hydrated");
            }

            if (weather.PrecipitationProbability > 70)
            {
                risks.Add("High chance of rain - prepare for wet conditions");
            }

            return risks.Any() ? string.Join(". ", risks) + "." : "No significant weather risks detected.";
        }
    }
}
