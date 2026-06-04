using System.Linq;

namespace CivicOps.Band
{
    /// <summary>
    /// Wire-friendly projections of Band types (enums as strings) used by both the
    /// REST endpoints and the SignalR stream, so the browser renders consistent
    /// data without depending on the host's JSON enum settings.
    /// </summary>
    public static class BandDto
    {
        public static object Message(BandMessage m) => new
        {
            id = m.Id,
            roomId = m.RoomId,
            senderId = m.SenderId,
            senderName = m.SenderName,
            senderKind = m.SenderKind.ToString(),
            senderColor = m.SenderColor,
            senderAvatar = m.SenderAvatar,
            kind = m.Kind.ToString(),
            text = m.Text,
            handoffTo = m.HandoffTo,
            data = m.Data,
            createdAt = m.CreatedAt,
            sequence = m.Sequence
        };

        public static object Room(BandRoom r) => new
        {
            id = r.Id,
            title = r.Title,
            reference = r.IncidentReference,
            area = r.Area,
            severity = r.Severity,
            phase = r.Phase,
            awaitingHumanConfirmation = r.AwaitingHumanConfirmation,
            isClosed = r.IsClosed,
            createdAt = r.CreatedAt,
            lastActivityAt = r.LastActivityAt,
            members = r.Members
        };

        public static object RoomView(BandRoomView v) => new
        {
            room = Room(v.Room),
            messages = v.Messages.Select(Message).ToList()
        };

        public static object Unit(ResponseUnit u) => new
        {
            id = u.Id,
            callSign = u.CallSign,
            type = u.TypeName,
            status = u.Status.ToString(),
            homeArea = u.HomeArea,
            lat = u.Latitude,
            lng = u.Longitude,
            activeAssignments = u.ActiveAssignments,
            skills = u.Skills
        };
    }
}
