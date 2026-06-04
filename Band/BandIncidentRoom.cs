using System.Collections.Generic;
using System.Linq;

namespace CivicOps.Band
{
    /// <summary>
    /// A typed read-model over a single per-incident Band room. Because the
    /// incident id IS the room id, this is the convenient lens for asking "what
    /// has happened to this incident so far?" without re-walking raw messages.
    /// </summary>
    public class BandIncidentRoom
    {
        private readonly IBandTransport _transport;

        public string RoomId { get; }

        public BandIncidentRoom(IBandTransport transport, string roomId)
        {
            _transport = transport;
            RoomId = roomId;
        }

        public BandRoom? Room => _transport.GetRoom(RoomId);

        public IReadOnlyList<BandMessage> Messages => _transport.GetMessages(RoomId);

        public BandMessage? RawReport => First(BandMessageKind.RawReport);
        public BandMessage? Classification => Last(BandMessageKind.Classified);
        public BandMessage? Proposal => Last(BandMessageKind.AssignmentProposed);
        public BandMessage? HumanDecision => Last(BandMessageKind.HumanDecision);
        public BandMessage? Dispatch => Last(BandMessageKind.Dispatched);
        public BandMessage? Resolution => Last(BandMessageKind.Resolved);
        public BandMessage? Summary => Last(BandMessageKind.Summary);

        public bool AwaitingHumanConfirmation => Room?.AwaitingHumanConfirmation ?? false;
        public bool IsClosed => Room?.IsClosed ?? false;
        public string Phase => Room?.Phase ?? "Unknown";

        /// <summary>Distinct agent identities that have collaborated in this room.</summary>
        public int CollaboratingAgentCount =>
            Messages.Where(m => m.SenderKind == BandParticipantKind.Agent)
                    .Select(m => m.SenderId).Distinct().Count();

        public int HandoffCount => Messages.Count(m => m.Kind == BandMessageKind.Handoff);

        private BandMessage? First(BandMessageKind kind) => Messages.FirstOrDefault(m => m.Kind == kind);
        private BandMessage? Last(BandMessageKind kind) => Messages.LastOrDefault(m => m.Kind == kind);
    }
}
