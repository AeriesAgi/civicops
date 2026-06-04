using System;
using System.Collections.Generic;
using System.Linq;
using CivicOps.Band.Agents;
using Microsoft.Extensions.Logging;

namespace CivicOps.Band
{
    /// <summary>
    /// Top-level facade over the Band coordination layer for the rest of CivicOps
    /// Command (controllers, SignalR, simulation). It owns the three agents (so
    /// they are alive and subscribed for the app's lifetime) and exposes the few
    /// operations the outside world performs *into* Band: dropping a raw report,
    /// recording a human dispatcher's decision, and reading room state.
    /// </summary>
    public class BandAgentService
    {
        private readonly IBandTransport _transport;
        private readonly BandOptions _options;
        private readonly ILogger<BandAgentService> _logger;

        // Constructor-injected so the agents are constructed and connected to Band.
        public BandAgentService(
            IBandTransport transport,
            BandOptions options,
            IncidentIntakeAgent intakeAgent,
            DispatchCoordinatorAgent dispatchAgent,
            ResponseMonitorAgent monitorAgent,
            ILogger<BandAgentService> logger)
        {
            _transport = transport;
            _options = options;
            _logger = logger;

            // Connect the well-known human identities too, so they appear as
            // first-class members of every room they post into.
            _transport.Connect(BandIdentities.Dispatcher);
            _transport.Connect(BandIdentities.Supervisor);
            _transport.Connect(BandIdentities.System);

            _logger.LogInformation(
                "Band coordination layer online in {Mode} mode with agents: {A1}, {A2}, {A3}",
                _options.Mode, intakeAgent.Identity.DisplayName,
                dispatchAgent.Identity.DisplayName, monitorAgent.Identity.DisplayName);
        }

        public string Mode => _options.Mode;
        public bool IsLive => _options.IsLive;

        /// <summary>
        /// A raw report enters the system. We open a per-incident Band room, post
        /// the raw report from the inbound channel identity, and let the agents
        /// take over through Band. Returns the room (incident) id.
        /// </summary>
        public string StartIncident(string rawText, string area, string channel = "Web")
        {
            var roomId = GenerateRoomId();
            var room = _transport.CreateRoom(roomId,
                title: $"Incoming {channel} report — {(string.IsNullOrWhiteSpace(area) ? "location TBC" : area)}",
                incidentReference: roomId,
                area: area);
            room.Phase = "Intake";

            var channelIdentity = new BandIdentity
            {
                Id = $"channel.{channel.ToLowerInvariant()}",
                DisplayName = $"{channel} Channel",
                Kind = BandParticipantKind.Human,
                Role = "Inbound Citizen Report",
                Avatar = "bi-megaphone",
                Color = "#94a3b8"
            };
            _transport.Connect(channelIdentity);

            _transport.Post(roomId, BandIdentities.System, BandMessageKind.System,
                $"Band room opened for incident {roomId}. Agents IncidentIntakeAgent, DispatchCoordinatorAgent and ResponseMonitorAgent are subscribed.");

            _transport.Post(roomId, channelIdentity, BandMessageKind.RawReport,
                rawText,
                new Dictionary<string, object?>
                {
                    ["rawText"] = rawText,
                    ["area"] = area,
                    ["channel"] = channel
                });

            return roomId;
        }

        /// <summary>The human dispatcher confirms, overrides or rejects a proposal,
        /// recorded as a first-class Band message in the room.</summary>
        public bool SubmitHumanDecision(string roomId, string decision, string? unitId, string? note, string? dispatcherName = null)
        {
            if (_transport.GetRoom(roomId) is null) return false;

            var dispatcher = BandIdentities.Dispatcher;
            if (!string.IsNullOrWhiteSpace(dispatcherName)) dispatcher.DisplayName = dispatcherName!;

            var text = decision.ToLowerInvariant() switch
            {
                "reject" => $"❌ Dispatcher REJECTED the proposed dispatch. {note}",
                "override" => $"🔁 Dispatcher OVERRODE the recommendation, selecting unit {unitId}. {note}",
                _ => $"✅ Dispatcher CONFIRMED the recommended dispatch{(string.IsNullOrWhiteSpace(unitId) ? "" : $" ({unitId})")}. {note}"
            };

            _transport.Post(roomId, dispatcher, BandMessageKind.HumanDecision, text.Trim(),
                new Dictionary<string, object?>
                {
                    ["decision"] = decision.ToLowerInvariant(),
                    ["unitId"] = unitId,
                    ["note"] = note
                });
            return true;
        }

        /// <summary>A supervisor acknowledges an escalation in-room (human-in-the-loop).</summary>
        public void PostSupervisorAck(string roomId, string note)
        {
            _transport.Post(roomId, BandIdentities.Supervisor, BandMessageKind.System,
                $"👍 Supervisor acknowledged escalation. {note}");
        }

        public BandRoomView? GetRoomView(string roomId)
        {
            var room = _transport.GetRoom(roomId);
            if (room is null) return null;
            return new BandRoomView
            {
                Room = room,
                Messages = _transport.GetMessages(roomId).ToList()
            };
        }

        public IReadOnlyList<BandRoom> ListRooms() => _transport.ListRooms();

        /// <summary>A typed read-model lens over one per-incident room.</summary>
        public BandIncidentRoom Incident(string roomId) => new(_transport, roomId);

        public IReadOnlyList<BandMessage> GetMessagesSince(string roomId, int afterSequence) =>
            _transport.GetMessages(roomId).Where(m => m.Sequence > afterSequence).ToList();

        private static string GenerateRoomId() =>
            $"INC-{DateTime.UtcNow:yyMMdd}-{Guid.NewGuid().ToString("N")[..5].ToUpperInvariant()}";
    }
}
