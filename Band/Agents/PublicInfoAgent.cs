using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CivicOps.Models;
using Microsoft.Extensions.Logging;

namespace CivicOps.Band.Agents
{
    /// <summary>
    /// AGENT 5 — the public-information specialist. It owns everything that faces
    /// the public: as soon as a unit is dispatched it notifies the citizen who
    /// reported, and for serious incidents it drafts a public area alert for the
    /// affected suburb (which a human still approves before broadcast). If the SLA
    /// slips it posts a transparent delay notice. This keeps citizen comms a clean,
    /// separately-owned lane in the same shared Band room rather than something the
    /// monitoring agent does as a side effect.
    /// </summary>
    public class PublicInfoAgent : BandAgent
    {
        public PublicInfoAgent(
            IBandTransport transport,
            ILogger<PublicInfoAgent> logger)
            : base(transport, BandIdentities.PublicInfoAgent, logger)
        {
        }

        protected override bool ShouldHandle(BandMessage message) =>
            message.Kind == BandMessageKind.Dispatched ||
            message.Kind == BandMessageKind.SlaWarning;

        protected override Task HandleAsync(BandMessage message)
        {
            JoinAndAnnounce(message.RoomId);
            return message.Kind == BandMessageKind.Dispatched
                ? OnDispatchedAsync(message)
                : OnSlaWarningAsync(message);
        }

        private Task OnDispatchedAsync(BandMessage dispatched)
        {
            var roomId = dispatched.RoomId;
            var reference = GetString(dispatched, "reference");
            var callSign = GetString(dispatched, "callSign");
            var area = GetString(dispatched, "area");
            var eta = (int)GetDouble(dispatched, "etaMinutes", 0);
            var severityStr = GetString(dispatched, "severity", "Medium");
            Enum.TryParse<IncidentPriority>(severityStr, out var priority);

            Band.Post(roomId, Identity, BandMessageKind.CitizenUpdate,
                $"📲 Citizen notification ({reference}): your report is confirmed and {callSign} has been dispatched to {area}, ETA ~{eta} min. You can track this reference in the Citizen App.");

            // For serious incidents, draft a public area alert for the suburb. A
            // human approves before it is broadcast — the agent only recommends it.
            if (DispatchMapping.RequiresEscalationPath(priority))
            {
                Band.Post(roomId, Identity, BandMessageKind.PublicAlert,
                    $"📢 DRAFT PUBLIC ALERT for {area} (awaiting human approval before broadcast):\n" +
                    $"\"{severityStr} incident response under way in {area}. Emergency units are on scene/en route — please keep access routes clear and follow official instructions.\"",
                    new Dictionary<string, object?>
                    {
                        ["area"] = area,
                        ["reference"] = reference,
                        ["severity"] = severityStr,
                        ["requiresHumanApproval"] = true
                    });
            }

            return Task.CompletedTask;
        }

        private Task OnSlaWarningAsync(BandMessage warning)
        {
            var roomId = warning.RoomId;
            var dispatched = Band.GetMessages(roomId).LastOrDefault(m => m.Kind == BandMessageKind.Dispatched);
            var reference = dispatched is not null ? GetString(dispatched, "reference") : string.Empty;

            Band.Post(roomId, Identity, BandMessageKind.CitizenUpdate,
                $"📲 Citizen notification ({reference}): we're prioritising your incident and bringing in additional support to reach you as fast as possible. Thank you for your patience.");

            return Task.CompletedTask;
        }
    }
}
