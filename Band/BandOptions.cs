namespace CivicOps.Band
{
    /// <summary>Runtime tuning for the Band coordination layer.</summary>
    public class BandOptions
    {
        /// <summary>"Simulation" (in-process broker, always works) or "Live"
        /// (also mirror messages to a hosted band.ai room).</summary>
        public string Mode { get; set; } = "Simulation";

        /// <summary>Base URL of the live Band API (used only in Live mode).</summary>
        public string ApiBaseUrl { get; set; } = "https://api.band.ai";

        /// <summary>API key for the live Band workspace (used only in Live mode).</summary>
        public string ApiKey { get; set; } = string.Empty;

        /// <summary>Band workspace / project the rooms live under.</summary>
        public string Workspace { get; set; } = "civicops-command";

        /// <summary>Seconds between ResponseMonitorAgent status heartbeats.</summary>
        public double TickSeconds { get; set; } = 2.5;

        public bool IsLive => string.Equals(Mode, "Live", System.StringComparison.OrdinalIgnoreCase)
                              && !string.IsNullOrWhiteSpace(ApiKey);
    }
}
