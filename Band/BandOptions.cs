namespace CivicOps.Band
{
    /// <summary>Runtime tuning for the Band coordination layer.</summary>
    public class BandOptions
    {
        /// <summary>"Simulation" (in-process broker, always works) or "Live"
        /// (also mirror messages to a hosted band.ai room).</summary>
        public string Mode { get; set; } = "Simulation";

        /// <summary>URL of the Node band-bridge sidecar that speaks @band-sdk/core
        /// to the real Band platform (used only in Live mode).</summary>
        public string BridgeUrl { get; set; } = "http://localhost:8787";

        /// <summary>Band workspace / project the rooms live under.</summary>
        public string Workspace { get; set; } = "civicops-command";

        /// <summary>Seconds between ResponseMonitorAgent status heartbeats.</summary>
        public double TickSeconds { get; set; } = 2.5;

        public bool IsLive => string.Equals(Mode, "Live", System.StringComparison.OrdinalIgnoreCase)
                              && !string.IsNullOrWhiteSpace(BridgeUrl);
    }
}
