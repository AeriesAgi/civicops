using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace CivicOps.Band.Agents
{
    /// <summary>
    /// AGENT 3 — the logistics specialist. While the DispatchCoordinatorAgent
    /// matches the primary responder, this agent works the room in parallel:
    /// on a serious classified incident it pre-stages a backup unit and mutual-aid
    /// resources, and when the ResponseMonitorAgent escalates an SLA risk it commits
    /// that backup through Band. It never dispatches the primary unit — that stays a
    /// human-confirmed decision — it only arranges supporting capacity, exactly like
    /// a real operations room. All coordination happens through Band messages.
    /// </summary>
    public class ResourceLogisticsAgent : BandAgent
    {
        private readonly IFleetService _fleet;

        public ResourceLogisticsAgent(
            IBandTransport transport,
            IFleetService fleet,
            ILogger<ResourceLogisticsAgent> logger)
            : base(transport, BandIdentities.LogisticsAgent, logger)
        {
            _fleet = fleet;
        }

        protected override bool ShouldHandle(BandMessage message) =>
            (message.Kind == BandMessageKind.Classified && IsSerious(message)) ||
            message.Kind == BandMessageKind.Escalation;

        protected override Task HandleAsync(BandMessage message)
        {
            JoinAndAnnounce(message.RoomId);
            return message.Kind == BandMessageKind.Classified
                ? PreStageAsync(message)
                : CommitBackupAsync(message);
        }

        // On a serious classified incident: identify a backup unit and mutual aid
        // alongside the primary, before anything is dispatched.
        private Task PreStageAsync(BandMessage classified)
        {
            var roomId = classified.RoomId;
            var area = GetString(classified, "area");
            var unitTypeName = GetString(classified, "requiredUnitType", nameof(UnitType.MetroPolice));
            Enum.TryParse<UnitType>(unitTypeName, out var requiredType);

            var (backup, mutualAid) = ResolveBackup(area, requiredType, excludeUnitId: null);

            Band.Post(roomId, Identity, BandMessageKind.ResourceStaged,
                $"PRE-STAGING SUPPORT for {requiredType} response in {area}.\n" +
                (backup is not null
                    ? $"Backup unit on standby: {backup.Unit.CallSign} ({backup.Unit.TypeName}, ~{backup.DistanceKm}km / ETA {backup.EtaMinutes}min)."
                    : "No same-type backup free — mutual aid will cover the gap.") + "\n" +
                $"Mutual aid: {mutualAid}.",
                new Dictionary<string, object?>
                {
                    ["backupUnitId"] = backup?.Unit.Id,
                    ["backupCallSign"] = backup?.Unit.CallSign,
                    ["mutualAid"] = mutualAid,
                    ["area"] = area,
                    ["requiredUnitType"] = requiredType.ToString()
                });

            return Task.CompletedTask;
        }

        // When the monitor escalates an SLA risk: commit the staged backup and
        // activate mutual aid, then hand the loop back to monitoring.
        private Task CommitBackupAsync(BandMessage escalation)
        {
            var roomId = escalation.RoomId;

            // Recover the incident's area + required type from the room's history.
            var classified = Band.GetMessages(roomId).LastOrDefault(m => m.Kind == BandMessageKind.Classified);
            var dispatched = Band.GetMessages(roomId).LastOrDefault(m => m.Kind == BandMessageKind.Dispatched);
            var area = GetString(classified ?? escalation, "area");
            var unitTypeName = GetString(classified ?? escalation, "requiredUnitType", nameof(UnitType.MetroPolice));
            Enum.TryParse<UnitType>(unitTypeName, out var requiredType);
            var primaryUnitId = dispatched is not null ? GetString(dispatched, "unitId") : null;

            var (backup, mutualAid) = ResolveBackup(area, requiredType, excludeUnitId: primaryUnitId);

            Band.Post(roomId, Identity, BandMessageKind.ResourceStaged,
                "ESCALATION RECEIVED — committing supporting capacity.\n" +
                (backup is not null
                    ? $"Backup {backup.Unit.CallSign} ({backup.Unit.TypeName}) moving to support, ETA {backup.EtaMinutes}min."
                    : "No same-type backup free — activating mutual aid only.") + "\n" +
                $"Mutual aid activated: {mutualAid}.",
                new Dictionary<string, object?>
                {
                    ["backupUnitId"] = backup?.Unit.Id,
                    ["backupCallSign"] = backup?.Unit.CallSign,
                    ["mutualAid"] = mutualAid
                });

            Band.Post(roomId, Identity, BandMessageKind.Handoff,
                "Backup and mutual aid staged. Handing back to ResponseMonitorAgent to keep tracking the SLA.",
                handoffTo: BandIdentities.MonitorAgent.Id);

            return Task.CompletedTask;
        }

        // Pick the best available unit as a backup, skipping the primary responder
        // so the agent never proposes the unit already being dispatched.
        private (UnitScore? backup, string mutualAid) ResolveBackup(string area, UnitType requiredType, string? excludeUnitId)
        {
            var (lat, lng) = _fleet.ResolveCoordinates(area);
            var backup = _fleet.ScoreUnits(requiredType, lat, lng, take: 4)
                .FirstOrDefault(s => !string.Equals(s.Unit.Id, excludeUnitId, StringComparison.OrdinalIgnoreCase));
            return (backup, DispatchMapping.MutualAidFor(requiredType));
        }

        private static bool IsSerious(BandMessage classified)
        {
            var severity = classified.Data.TryGetValue("severity", out var v) ? v?.ToString() : null;
            return Enum.TryParse<Models.IncidentPriority>(severity, out var priority)
                   && DispatchMapping.RequiresEscalationPath(priority);
        }
    }
}
