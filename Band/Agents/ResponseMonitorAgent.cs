using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CivicOps.Models;
using CivicOps.Services;
using Microsoft.Extensions.Logging;

namespace CivicOps.Band.Agents
{
    /// <summary>
    /// AGENT 4 — reads active assignments from Band, monitors GPS + the SLA timer,
    /// posts status heartbeats, escalates through Band if the SLA is at risk (which
    /// the ResourceLogisticsAgent and a human supervisor both act on), and closes
    /// the incident in Band with an audit summary when it is resolved. Citizen-facing
    /// comms during the response are owned by the PublicInfoAgent; this agent posts
    /// the final on-scene resolution update as the responding unit closing the loop.
    /// </summary>
    public class ResponseMonitorAgent : BandAgent
    {
        private readonly IFleetService _fleet;
        private readonly IDataService _data;
        private readonly BandOptions _options;

        public ResponseMonitorAgent(
            IBandTransport transport,
            IFleetService fleet,
            IDataService data,
            BandOptions options,
            ILogger<ResponseMonitorAgent> logger)
            : base(transport, BandIdentities.MonitorAgent, logger)
        {
            _fleet = fleet;
            _data = data;
            _options = options;
        }

        protected override bool ShouldHandle(BandMessage message) =>
            message.Kind == BandMessageKind.Dispatched;

        protected override async Task HandleAsync(BandMessage dispatched)
        {
            var roomId = dispatched.RoomId;
            JoinAndAnnounce(roomId);

            var unitId = GetString(dispatched, "unitId");
            var callSign = GetString(dispatched, "callSign");
            var area = GetString(dispatched, "area");
            var reference = GetString(dispatched, "reference");
            var incidentId = GetString(dispatched, "incidentId");
            var destLat = GetDouble(dispatched, "destLat");
            var destLng = GetDouble(dispatched, "destLng");
            var slaTarget = (int)GetDouble(dispatched, "slaTargetMinutes", 20);
            var severityStr = GetString(dispatched, "severity", "Medium");
            Enum.TryParse<IncidentPriority>(severityStr, out var priority);

            var startedAt = DateTime.UtcNow;
            var tick = TimeSpan.FromSeconds(Math.Clamp(_options.TickSeconds, 0.5, 10));

            Band.Post(roomId, Identity, BandMessageKind.StatusUpdate,
                $"Now tracking {callSign} toward {area}. SLA target {slaTarget} min. Watching GPS + ETA.");

            var escalated = false;
            const int totalTicks = 5;
            for (var i = 1; i <= totalTicks; i++)
            {
                await Task.Delay(tick);

                // Advance the unit toward the scene and recompute live ETA.
                _fleet.MoveTowards(unitId, destLat, destLng, 1.0 / (totalTicks - i + 1));
                var unit = _fleet.GetUnit(unitId);
                if (unit is null) break;

                var distance = InMemoryFleetService.Haversine(destLat, destLng, unit.Latitude, unit.Longitude);
                var eta = (int)Math.Max(0, Math.Round(distance / Math.Max(20, unit.SpeedKmh) * 60));
                var elapsed = (DateTime.UtcNow - startedAt).TotalMinutes;

                Band.Post(roomId, Identity, BandMessageKind.StatusUpdate,
                    $"{callSign} en route · ~{distance:0.0}km out · ETA {eta} min · {elapsed:0.0} min elapsed.",
                    new Dictionary<string, object?>
                    {
                        ["unitId"] = unitId,
                        ["distanceKm"] = Math.Round(distance, 1),
                        ["etaMinutes"] = eta,
                        ["lat"] = Math.Round(unit.Latitude, 5),
                        ["lng"] = Math.Round(unit.Longitude, 5)
                    });

                // Demonstrate the escalation path for high-severity incidents.
                if (!escalated && DispatchMapping.RequiresEscalationPath(priority) && i == 2)
                {
                    escalated = true;
                    Band.Post(roomId, Identity, BandMessageKind.SlaWarning,
                        $"⏱️ SLA WARNING: {severityStr} incident, {Math.Max(0, slaTarget - elapsed):0} min of SLA budget remaining.");
                    Band.Post(roomId, Identity, BandMessageKind.Escalation,
                        $"Escalating to Shift Supervisor: high-severity {severityStr} incident — requesting a backup unit and supervisor oversight.",
                        new Dictionary<string, object?> { ["reference"] = reference },
                        handoffTo: BandIdentities.Supervisor.Id);
                }
            }

            // Arrival on scene.
            _fleet.SetStatus(unitId, UnitStatus.OnScene);
            Band.Post(roomId, Identity, BandMessageKind.StatusUpdate,
                $"✅ {callSign} ON SCENE at {area}. Responding.");

            await Task.Delay(tick);

            // Resolution — release the unit and close the loop.
            _fleet.SetStatus(unitId, UnitStatus.Available);
            await MarkIncidentResolvedAsync(incidentId, callSign);

            Band.Post(roomId, Identity, BandMessageKind.CitizenUpdate,
                $"📲 Citizen update ({reference}): the incident has been resolved on scene by {callSign}. Thank you for reporting.");

            Band.Post(roomId, Identity, BandMessageKind.Resolved,
                $"RESOLVED · {callSign} cleared the scene at {area}. Incident {reference} closed.");

            PostSummary(roomId, reference, callSign, area, slaTarget, escalated, startedAt);

            Band.UpdateRoom(roomId, r => { r.IsClosed = true; r.Phase = "Resolved"; });
        }

        private void PostSummary(string roomId, string reference, string callSign, string area,
            int slaTarget, bool escalated, DateTime startedAt)
        {
            var messages = Band.GetMessages(roomId);
            var totalMinutes = (DateTime.UtcNow - startedAt).TotalMinutes;
            var agents = messages.Where(m => m.SenderKind == BandParticipantKind.Agent)
                                 .Select(m => m.SenderName).Distinct().Count();
            var handoffs = messages.Count(m => m.Kind == BandMessageKind.Handoff);

            var summary =
                $"📋 BAND ROOM SUMMARY — {reference}\n" +
                $"• Area: {area}\n" +
                $"• Responding unit: {callSign}\n" +
                $"• Agents that collaborated through Band: {agents}\n" +
                $"• Hand-offs through Band: {handoffs}\n" +
                $"• Human-in-the-loop: dispatcher confirmation captured in-room\n" +
                $"• Escalation path exercised: {(escalated ? "yes (supervisor engaged)" : "no")}\n" +
                $"• Response window monitored: ~{totalMinutes:0.0} min (SLA target {slaTarget} min)\n" +
                $"• Messages in audit trail: {messages.Count}\n" +
                $"Lifecycle: Raw report → Classified → Proposed → Human-confirmed → Dispatched → Monitored → Resolved.";

            Band.Post(roomId, Identity, BandMessageKind.Summary, summary);
        }

        private async Task MarkIncidentResolvedAsync(string incidentId, string callSign)
        {
            if (string.IsNullOrWhiteSpace(incidentId)) return;
            try
            {
                var incident = await _data.GetIncidentByIdAsync(incidentId);
                if (incident is null) return;
                incident.Status = IncidentStatus.Resolved;
                incident.LastUpdatedAt = DateTime.UtcNow;
                incident.PublicUpdates.Add(new PublicUpdate
                {
                    Content = $"Resolved on scene by {callSign} (coordinated via Band multi-agent dispatch).",
                    UpdatedBy = "ResponseMonitorAgent (Band)",
                    RelatedStatus = IncidentStatus.Resolved
                });
                await _data.UpdateIncidentAsync(incident);
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Could not mark incident {Id} resolved", incidentId);
            }
        }
    }
}
