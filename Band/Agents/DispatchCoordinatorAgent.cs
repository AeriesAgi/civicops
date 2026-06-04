using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace CivicOps.Band.Agents
{
    /// <summary>
    /// AGENT 2 — reads the classified incident from Band, queries available units
    /// by type and proximity, scores the best match (skill + ETA + workload),
    /// proposes an assignment with reasoning, and waits for a human dispatcher to
    /// confirm — then dispatches and notifies the ResponseMonitorAgent. All
    /// coordination happens through Band.
    /// </summary>
    public class DispatchCoordinatorAgent : BandAgent
    {
        private readonly IFleetService _fleet;

        public DispatchCoordinatorAgent(
            IBandTransport transport,
            IFleetService fleet,
            ILogger<DispatchCoordinatorAgent> logger)
            : base(transport, BandIdentities.DispatchAgent, logger)
        {
            _fleet = fleet;
        }

        protected override bool ShouldHandle(BandMessage message) =>
            message.Kind == BandMessageKind.Classified ||
            message.Kind == BandMessageKind.HumanDecision;

        protected override async Task HandleAsync(BandMessage message)
        {
            JoinAndAnnounce(message.RoomId);
            if (message.Kind == BandMessageKind.Classified)
                await ProposeAssignmentAsync(message);
            else
                await ConfirmDispatchAsync(message);
        }

        private Task ProposeAssignmentAsync(BandMessage classified)
        {
            var roomId = classified.RoomId;
            var area = GetString(classified, "area");
            var severity = GetString(classified, "severity", "Medium");
            var unitTypeName = GetString(classified, "requiredUnitType", nameof(UnitType.MetroPolice));
            Enum.TryParse<UnitType>(unitTypeName, out var requiredType);

            var (lat, lng) = _fleet.ResolveCoordinates(area);
            var scored = _fleet.ScoreUnits(requiredType, lat, lng, take: 4);

            if (scored.Count == 0)
            {
                Band.Post(roomId, Identity, BandMessageKind.System,
                    "No available units to match. Escalating capacity issue to supervisor.");
                Band.Post(roomId, Identity, BandMessageKind.Escalation,
                    "No free units of any type — supervisor intervention required.",
                    handoffTo: BandIdentities.Supervisor.Id);
                return Task.CompletedTask;
            }

            Band.Post(roomId, Identity, BandMessageKind.UnitsQueried,
                $"Queried fleet for '{requiredType}' near {area}. {scored.Count} candidate(s) evaluated:\n" +
                string.Join("\n", scored.Select((s, i) => $"  {i + 1}. {s.Reasoning} → score {s.Score:0.00}")),
                new Dictionary<string, object?>
                {
                    ["candidates"] = scored.Select(s => new Dictionary<string, object?>
                    {
                        ["unitId"] = s.Unit.Id,
                        ["callSign"] = s.Unit.CallSign,
                        ["type"] = s.Unit.TypeName,
                        ["distanceKm"] = s.DistanceKm,
                        ["etaMinutes"] = s.EtaMinutes,
                        ["score"] = s.Score
                    }).ToList()
                });

            var best = scored[0];
            var alt = scored.Skip(1).FirstOrDefault();

            var reasoning =
                $"Recommend {best.Unit.CallSign} ({best.Unit.TypeName}). " +
                $"Closest qualified unit at ~{best.DistanceKm}km / ETA {best.EtaMinutes}min, " +
                $"skill match {best.SkillMatch:P0}, current workload {best.Unit.ActiveAssignments}. " +
                (alt is not null ? $"Fallback: {alt.Unit.CallSign} (ETA {alt.EtaMinutes}min)." : "No fallback available.");

            Band.UpdateRoom(roomId, r =>
            {
                r.AwaitingHumanConfirmation = true;
                r.Phase = "Awaiting Human Confirmation";
            });

            Band.Post(roomId, Identity, BandMessageKind.AssignmentProposed,
                $"PROPOSED DISPATCH · {best.Unit.CallSign}\n{reasoning}\n" +
                $"🧑‍✈️ Awaiting human dispatcher confirmation in this Band room.",
                new Dictionary<string, object?>
                {
                    ["requiresHumanConfirmation"] = true,
                    ["recommendedUnitId"] = best.Unit.Id,
                    ["recommendedCallSign"] = best.Unit.CallSign,
                    ["recommendedType"] = best.Unit.TypeName,
                    ["etaMinutes"] = best.EtaMinutes,
                    ["confidence"] = best.Score,
                    ["alternativeUnitId"] = alt?.Unit.Id,
                    ["alternativeCallSign"] = alt?.Unit.CallSign,
                    ["area"] = area,
                    ["severity"] = severity,
                    ["incidentId"] = GetString(classified, "incidentId"),
                    ["reference"] = GetString(classified, "reference"),
                    ["slaTargetMinutes"] = GetString(classified, "slaTargetMinutes", "20")
                });

            return Task.CompletedTask;
        }

        private Task ConfirmDispatchAsync(BandMessage decision)
        {
            var roomId = decision.RoomId;
            var verdict = GetString(decision, "decision", "confirm");
            var chosenUnitId = GetString(decision, "unitId");

            var proposal = LastOfKind(roomId, BandMessageKind.AssignmentProposed);
            if (proposal is null)
            {
                Band.Post(roomId, Identity, BandMessageKind.System,
                    "Received a human decision but found no proposal to act on.");
                return Task.CompletedTask;
            }

            if (string.Equals(verdict, "reject", StringComparison.OrdinalIgnoreCase))
            {
                Band.UpdateRoom(roomId, r => { r.AwaitingHumanConfirmation = false; r.Phase = "Rejected — re-queue"; });
                Band.Post(roomId, Identity, BandMessageKind.System,
                    "Dispatcher rejected the proposal. Holding incident for manual re-assignment.");
                return Task.CompletedTask;
            }

            // Confirm or override: honour the human's chosen unit if supplied.
            if (string.IsNullOrWhiteSpace(chosenUnitId))
                chosenUnitId = GetString(proposal, "recommendedUnitId");

            var unit = _fleet.GetUnit(chosenUnitId);
            if (unit is null)
            {
                Band.Post(roomId, Identity, BandMessageKind.System,
                    $"Confirmed unit {chosenUnitId} not found in fleet.");
                return Task.CompletedTask;
            }

            var area = GetString(proposal, "area");
            var (lat, lng) = _fleet.ResolveCoordinates(area);
            var distance = InMemoryFleetService.Haversine(lat, lng, unit.Latitude, unit.Longitude);
            var eta = (int)Math.Max(1, Math.Round(distance / Math.Max(20, unit.SpeedKmh) * 60));

            _fleet.SetStatus(unit.Id, UnitStatus.Dispatched);

            Band.UpdateRoom(roomId, r => { r.AwaitingHumanConfirmation = false; r.Phase = "Dispatched"; });

            var overridden = !string.Equals(chosenUnitId, GetString(proposal, "recommendedUnitId"), StringComparison.OrdinalIgnoreCase);

            Band.Post(roomId, Identity, BandMessageKind.Dispatched,
                $"DISPATCHED · {unit.CallSign} ({unit.TypeName}) is EN ROUTE to {area}. ETA {eta} min." +
                (overridden ? " (dispatcher overrode the recommended unit)." : " (dispatcher confirmed recommendation)."),
                new Dictionary<string, object?>
                {
                    ["unitId"] = unit.Id,
                    ["callSign"] = unit.CallSign,
                    ["unitType"] = unit.TypeName,
                    ["etaMinutes"] = eta,
                    ["area"] = area,
                    ["destLat"] = lat,
                    ["destLng"] = lng,
                    ["severity"] = GetString(proposal, "severity"),
                    ["slaTargetMinutes"] = GetString(proposal, "slaTargetMinutes", "20"),
                    ["incidentId"] = GetString(proposal, "incidentId"),
                    ["reference"] = GetString(proposal, "reference")
                });

            Band.Post(roomId, Identity, BandMessageKind.Handoff,
                $"Handing off to ResponseMonitorAgent to track {unit.CallSign} against the SLA timer.",
                handoffTo: BandIdentities.MonitorAgent.Id);

            return Task.CompletedTask;
        }

        private BandMessage? LastOfKind(string roomId, BandMessageKind kind) =>
            Band.GetMessages(roomId).LastOrDefault(m => m.Kind == kind);
    }
}
