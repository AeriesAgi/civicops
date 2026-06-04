using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace CivicOps.Band
{
    public enum UnitType
    {
        ArmedResponse,
        Ambulance,
        FireRescue,
        MetroPolice,
        DisasterManagement,
        UtilityCrew
    }

    public enum UnitStatus
    {
        Available,
        Dispatched,
        OnScene,
        OutOfService
    }

    /// <summary>A deployable response unit in the CivicOps Command fleet.</summary>
    public class ResponseUnit
    {
        public string Id { get; set; } = string.Empty;
        public string CallSign { get; set; } = string.Empty;
        public UnitType Type { get; set; }
        public UnitStatus Status { get; set; } = UnitStatus.Available;
        public string HomeArea { get; set; } = string.Empty;
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double SpeedKmh { get; set; } = 45;
        /// <summary>Open assignments the unit currently carries (workload).</summary>
        public int ActiveAssignments { get; set; }
        public List<string> Skills { get; set; } = new();
        public string TypeName => Type switch
        {
            UnitType.ArmedResponse => "Armed Response",
            UnitType.Ambulance => "EMS / Ambulance",
            UnitType.FireRescue => "Fire & Rescue",
            UnitType.MetroPolice => "Metro Police",
            UnitType.DisasterManagement => "Disaster Management",
            UnitType.UtilityCrew => "Utility Crew",
            _ => Type.ToString()
        };
    }

    /// <summary>The scored evaluation of a unit against an incident.</summary>
    public class UnitScore
    {
        public ResponseUnit Unit { get; set; } = new();
        public double DistanceKm { get; set; }
        public int EtaMinutes { get; set; }
        public double SkillMatch { get; set; }
        public double WorkloadPenalty { get; set; }
        public double Score { get; set; }
        public string Reasoning { get; set; } = string.Empty;
    }

    public interface IFleetService
    {
        IReadOnlyList<ResponseUnit> GetAllUnits();
        (double lat, double lng) ResolveCoordinates(string area);
        IReadOnlyList<UnitScore> ScoreUnits(UnitType requiredType, double lat, double lng, int take = 5);
        ResponseUnit? GetUnit(string id);
        void SetStatus(string unitId, UnitStatus status);
        void MoveTowards(string unitId, double destLat, double destLng, double fractionOfRemaining);
    }

    /// <summary>
    /// In-memory fleet seeded with a realistic eThekwini (Durban) response fleet.
    /// Keeps the Command demo dependency-free while modelling exactly the data the
    /// DispatchCoordinatorAgent needs: type, proximity, ETA and live workload.
    /// </summary>
    public class InMemoryFleetService : IFleetService
    {
        private readonly ConcurrentDictionary<string, ResponseUnit> _units = new();

        // Approximate coordinates for the demo suburbs used across CivicOps.
        private static readonly Dictionary<string, (double lat, double lng)> AreaCoords =
            new(StringComparer.OrdinalIgnoreCase)
            {
                ["Chatsworth"] = (-29.9094, 30.8853),
                ["Phoenix"] = (-29.7000, 30.9700),
                ["Durban CBD"] = (-29.8587, 31.0218),
                ["Umlazi"] = (-29.9667, 30.8833),
                ["KwaMashu"] = (-29.7400, 30.9900),
                ["Pinetown"] = (-29.8150, 30.8600),
                ["Amanzimtoti"] = (-30.0500, 30.8800),
                ["Westville"] = (-29.8333, 30.9270),
                ["Berea"] = (-29.8480, 31.0050),
                ["Bluff"] = (-29.9300, 31.0100),
                ["Umhlanga"] = (-29.7270, 31.0840),
                ["Inanda"] = (-29.7000, 30.9300),
                ["Isipingo"] = (-29.9900, 30.9300),
                ["eThekwini"] = (-29.8587, 31.0218)
            };

        public InMemoryFleetService()
        {
            Seed();
        }

        private void Seed()
        {
            var seed = new[]
            {
                new ResponseUnit { Id = "U-AR1", CallSign = "Alpha-1", Type = UnitType.ArmedResponse, HomeArea = "Chatsworth", Skills = { "armed-response", "patrol" } },
                new ResponseUnit { Id = "U-AR2", CallSign = "Alpha-2", Type = UnitType.ArmedResponse, HomeArea = "Umlazi", Skills = { "armed-response", "patrol" } },
                new ResponseUnit { Id = "U-FR1", CallSign = "Fire-1", Type = UnitType.FireRescue, HomeArea = "Durban CBD", Skills = { "fire", "rescue", "hazmat" } },
                new ResponseUnit { Id = "U-FR2", CallSign = "Fire-2", Type = UnitType.FireRescue, HomeArea = "Pinetown", Skills = { "fire", "rescue" } },
                new ResponseUnit { Id = "U-EMS1", CallSign = "Medic-1", Type = UnitType.Ambulance, HomeArea = "Chatsworth", Skills = { "als", "trauma" } },
                new ResponseUnit { Id = "U-EMS2", CallSign = "Medic-2", Type = UnitType.Ambulance, HomeArea = "Berea", Skills = { "bls" } },
                new ResponseUnit { Id = "U-MP1", CallSign = "Metro-1", Type = UnitType.MetroPolice, HomeArea = "Durban CBD", Skills = { "traffic", "crowd-control" } },
                new ResponseUnit { Id = "U-DM1", CallSign = "Disaster-1", Type = UnitType.DisasterManagement, HomeArea = "Westville", Skills = { "flood", "evacuation", "coordination" } },
                new ResponseUnit { Id = "U-UT1", CallSign = "Utility-1", Type = UnitType.UtilityCrew, HomeArea = "Phoenix", Skills = { "water", "electricity" } }
            };

            foreach (var u in seed)
            {
                var (lat, lng) = ResolveCoordinates(u.HomeArea);
                // jitter slightly so units aren't stacked on one point
                u.Latitude = lat + (Random.Shared.NextDouble() - 0.5) * 0.02;
                u.Longitude = lng + (Random.Shared.NextDouble() - 0.5) * 0.02;
                _units[u.Id] = u;
            }
        }

        public IReadOnlyList<ResponseUnit> GetAllUnits() => _units.Values.OrderBy(u => u.CallSign).ToList();

        public ResponseUnit? GetUnit(string id) => _units.TryGetValue(id, out var u) ? u : null;

        public (double lat, double lng) ResolveCoordinates(string area)
        {
            if (!string.IsNullOrWhiteSpace(area))
            {
                foreach (var kvp in AreaCoords)
                {
                    if (area.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase))
                        return kvp.Value;
                }
            }
            return AreaCoords["eThekwini"];
        }

        public IReadOnlyList<UnitScore> ScoreUnits(UnitType requiredType, double lat, double lng, int take = 5)
        {
            var candidates = _units.Values
                .Where(u => u.Type == requiredType && u.Status == UnitStatus.Available)
                .ToList();

            // Fallback: if no exact-type unit is free, widen to any available unit.
            if (candidates.Count == 0)
            {
                candidates = _units.Values.Where(u => u.Status == UnitStatus.Available).ToList();
            }

            var scored = candidates.Select(u =>
            {
                var distance = Haversine(lat, lng, u.Latitude, u.Longitude);
                var eta = (int)Math.Max(1, Math.Round(distance / Math.Max(20, u.SpeedKmh) * 60));
                var skillMatch = u.Type == requiredType ? 1.0 : 0.5;
                var workloadPenalty = u.ActiveAssignments * 0.15;
                // Lower ETA is better; combine into a 0-1 desirability score.
                var etaScore = 1.0 / (1.0 + eta / 6.0);
                var score = Math.Clamp((etaScore * 0.55) + (skillMatch * 0.35) - workloadPenalty, 0, 1);

                return new UnitScore
                {
                    Unit = u,
                    DistanceKm = Math.Round(distance, 1),
                    EtaMinutes = eta,
                    SkillMatch = skillMatch,
                    WorkloadPenalty = workloadPenalty,
                    Score = Math.Round(score, 3),
                    Reasoning = $"{u.CallSign} ({u.TypeName}) ~{Math.Round(distance, 1)}km / ETA {eta}min, " +
                                $"skill match {skillMatch:P0}, workload {u.ActiveAssignments} open."
                };
            })
            .OrderByDescending(s => s.Score)
            .ThenBy(s => s.EtaMinutes)
            .Take(take)
            .ToList();

            return scored;
        }

        public void SetStatus(string unitId, UnitStatus status)
        {
            if (_units.TryGetValue(unitId, out var u))
            {
                if (status == UnitStatus.Dispatched && u.Status != UnitStatus.Dispatched)
                    u.ActiveAssignments++;
                if (status == UnitStatus.Available && u.ActiveAssignments > 0)
                    u.ActiveAssignments--;
                u.Status = status;
            }
        }

        public void MoveTowards(string unitId, double destLat, double destLng, double fractionOfRemaining)
        {
            if (_units.TryGetValue(unitId, out var u))
            {
                u.Latitude += (destLat - u.Latitude) * fractionOfRemaining;
                u.Longitude += (destLng - u.Longitude) * fractionOfRemaining;
            }
        }

        public static double Haversine(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6371.0; // km
            double dLat = ToRad(lat2 - lat1);
            double dLon = ToRad(lon2 - lon1);
            double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                       Math.Cos(ToRad(lat1)) * Math.Cos(ToRad(lat2)) *
                       Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            return R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        }

        private static double ToRad(double deg) => deg * Math.PI / 180.0;
    }
}
