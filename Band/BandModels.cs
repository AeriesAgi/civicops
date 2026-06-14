using System;
using System.Collections.Generic;

namespace CivicOps.Band
{
    /// <summary>
    /// The kind of participant that authored a Band message. Band is a shared
    /// interaction layer, so every message is attributed to an identity that is
    /// either an autonomous agent, a human operator, or the platform itself.
    /// </summary>
    public enum BandParticipantKind
    {
        Agent,
        Human,
        System
    }

    /// <summary>
    /// Semantic type of a Band message. These types are how the agents reason
    /// about the conversation: each agent reacts to specific kinds and emits
    /// others, so the workflow advances purely by messages flowing through Band
    /// (never by direct method calls between agents).
    /// </summary>
    public enum BandMessageKind
    {
        System,
        AgentJoined,
        RawReport,          // citizen / channel report enters the room
        Classified,         // IncidentIntakeAgent output
        Handoff,            // explicit hand-off to the next agent
        UnitsQueried,       // DispatchCoordinatorAgent telemetry
        AssignmentProposed, // DispatchCoordinatorAgent recommends a unit (awaits human)
        HumanDecision,      // dispatcher confirms / overrides / rejects
        Dispatched,         // unit confirmed and en route
        ResourceStaged,     // ResourceLogisticsAgent stages backup / mutual aid
        StatusUpdate,       // ResponseMonitorAgent heartbeat / GPS / ETA
        SlaWarning,         // SLA timer at risk
        Escalation,         // escalated to supervisor / logistics
        PublicAlert,        // PublicInfoAgent recommends a public area alert
        CitizenUpdate,      // status pushed back to the citizen
        Resolved,           // incident resolved on scene
        Summary             // closing audit summary of the room
    }

    /// <summary>
    /// A Band identity. Each agent and each human connects to Band under its own
    /// identity, exactly like a distinct member of a chat room.
    /// </summary>
    public class BandIdentity
    {
        public string Id { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public BandParticipantKind Kind { get; set; } = BandParticipantKind.Agent;
        public string Role { get; set; } = string.Empty;
        public string Avatar { get; set; } = "bi-robot";
        public string Color { get; set; } = "#38bdf8";
    }

    /// <summary>
    /// A single message in a Band room. This is the atomic unit of coordination
    /// and the audit record judges can replay end to end.
    /// </summary>
    public class BandMessage
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string RoomId { get; set; } = string.Empty;
        public string SenderId { get; set; } = string.Empty;
        public string SenderName { get; set; } = string.Empty;
        public BandParticipantKind SenderKind { get; set; } = BandParticipantKind.Agent;
        public string SenderColor { get; set; } = "#38bdf8";
        public string SenderAvatar { get; set; } = "bi-robot";
        public BandMessageKind Kind { get; set; } = BandMessageKind.System;
        public string Text { get; set; } = string.Empty;
        /// <summary>Optional identity id this message hands work off to.</summary>
        public string? HandoffTo { get; set; }
        /// <summary>Structured payload carried with the message (JSON-friendly).</summary>
        public Dictionary<string, object?> Data { get; set; } = new();
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public int Sequence { get; set; }
    }

    /// <summary>
    /// A per-incident Band room. The incident id IS the room id, so every signal
    /// about an incident lives in exactly one shared space.
    /// </summary>
    public class BandRoom
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string IncidentReference { get; set; } = string.Empty;
        public string Area { get; set; } = string.Empty;
        public string Severity { get; set; } = "Pending";
        public string Phase { get; set; } = "Intake";
        public bool AwaitingHumanConfirmation { get; set; }
        public bool IsClosed { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime LastActivityAt { get; set; } = DateTime.UtcNow;
        public List<string> Members { get; set; } = new();
    }

    public class BandRoomView
    {
        public BandRoom Room { get; set; } = new();
        public List<BandMessage> Messages { get; set; } = new();
    }

    public class BandMessageEventArgs : EventArgs
    {
        public BandMessage Message { get; }
        public BandMessageEventArgs(BandMessage message) => Message = message;
    }
}
