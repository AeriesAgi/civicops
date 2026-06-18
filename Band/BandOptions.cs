namespace CivicOps.Band
{
    /// <summary>Runtime tuning for the Band coordination layer.</summary>
    public class BandOptions
    {
        /// <summary>"Simulation" (in-process broker, always works) or "Live"
        /// (also mirror messages to a hosted band.ai room).</summary>
        public string Mode { get; set; } = "Simulation";

        /// <summary>URL of the Node band-bridge sidecar that speaks the official
        /// Band SDK (@band-ai/sdk) to the real Band platform (Live mode only).</summary>
        public string BridgeUrl { get; set; } = "http://localhost:8787";

        /// <summary>Band workspace / project the rooms live under.</summary>
        public string Workspace { get; set; } = "civicops-command";

        /// <summary>Hosted Band REST base URL used by the optional Node bridge.
        /// Stored for readiness/status only; API keys are never exposed.</summary>
        public string ApiBaseUrl { get; set; } = "https://app.thenvoi.com";

        /// <summary>True when BAND_API_KEY is present in the server environment.</summary>
        public bool ApiKeyConfigured { get; set; }

        /// <summary>True when deterministic demo mode is explicitly requested.</summary>
        public bool DemoMode { get; set; } = true;

        /// <summary>Seconds between ResponseMonitorAgent status heartbeats.</summary>
        public double TickSeconds { get; set; } = 2.5;

        public bool IsLive => !DemoMode
                              && string.Equals(Mode, "Live", System.StringComparison.OrdinalIgnoreCase)
                              && !string.IsNullOrWhiteSpace(BridgeUrl);
    }
}
