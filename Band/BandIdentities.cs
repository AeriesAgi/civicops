namespace CivicOps.Band
{
    /// <summary>
    /// The well-known Band identities for the dispatch workflow. Each agent and
    /// each human role connects to Band under its own identity, so the room
    /// transcript reads like a real coordinated team.
    /// </summary>
    public static class BandIdentities
    {
        public static BandIdentity IntakeAgent => new()
        {
            Id = "agent.intake",
            DisplayName = "IncidentIntakeAgent",
            Kind = BandParticipantKind.Agent,
            Role = "Intake & Classification",
            Avatar = "bi-clipboard2-pulse",
            Color = "#38bdf8"
        };

        public static BandIdentity DispatchAgent => new()
        {
            Id = "agent.dispatch",
            DisplayName = "DispatchCoordinatorAgent",
            Kind = BandParticipantKind.Agent,
            Role = "Unit Matching & Coordination",
            Avatar = "bi-diagram-3",
            Color = "#a855f7"
        };

        public static BandIdentity LogisticsAgent => new()
        {
            Id = "agent.logistics",
            DisplayName = "ResourceLogisticsAgent",
            Kind = BandParticipantKind.Agent,
            Role = "Backup & Mutual-Aid Staging",
            Avatar = "bi-box-seam",
            Color = "#14b8a6"
        };

        public static BandIdentity MonitorAgent => new()
        {
            Id = "agent.monitor",
            DisplayName = "ResponseMonitorAgent",
            Kind = BandParticipantKind.Agent,
            Role = "Monitoring & Escalation",
            Avatar = "bi-activity",
            Color = "#f97316"
        };

        public static BandIdentity PublicInfoAgent => new()
        {
            Id = "agent.publicinfo",
            DisplayName = "PublicInfoAgent",
            Kind = BandParticipantKind.Agent,
            Role = "Citizen Notifications & Public Alerts",
            Avatar = "bi-megaphone",
            Color = "#ec4899"
        };

        public static BandIdentity Dispatcher => new()
        {
            Id = "human.dispatcher",
            DisplayName = "Human Dispatcher",
            Kind = BandParticipantKind.Human,
            Role = "Dispatch Authority (human-in-the-loop)",
            Avatar = "bi-person-badge",
            Color = "#22c55e"
        };

        public static BandIdentity Supervisor => new()
        {
            Id = "human.supervisor",
            DisplayName = "Shift Supervisor",
            Kind = BandParticipantKind.Human,
            Role = "Escalation Authority",
            Avatar = "bi-person-vcard",
            Color = "#eab308"
        };

        public static BandIdentity System => new()
        {
            Id = "system.band",
            DisplayName = "Band",
            Kind = BandParticipantKind.System,
            Role = "Interaction Layer",
            Avatar = "bi-broadcast-pin",
            Color = "#64748b"
        };
    }
}
