using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace CivicOps.Band
{
    /// <summary>
    /// Optional live mirror to a hosted band.ai workspace. When Band:Mode=Live and
    /// an API key is configured, every message the agents publish locally is also
    /// best-effort relayed to the real Band REST API, so the same multi-agent
    /// transcript lands in a hosted Band room. The local broker stays the source
    /// of truth, which guarantees the demo runs even with no network — this layer
    /// is purely additive. Mirrors the shape of the @band-sdk/core REST surface.
    /// </summary>
    public class BandHttpGateway
    {
        private readonly IHttpClientFactory _httpFactory;
        private readonly BandOptions _options;
        private readonly ILogger<BandHttpGateway> _logger;

        public BandHttpGateway(
            IBandTransport transport,
            IHttpClientFactory httpFactory,
            BandOptions options,
            ILogger<BandHttpGateway> logger)
        {
            _httpFactory = httpFactory;
            _options = options;
            _logger = logger;

            if (_options.IsLive)
            {
                transport.MessagePosted += OnMessagePosted;
                _logger.LogInformation("Band live mirror enabled → {Url} (workspace {Ws})",
                    _options.ApiBaseUrl, _options.Workspace);
            }
        }

        private void OnMessagePosted(object? sender, BandMessageEventArgs e)
        {
            // Fire-and-forget; never let live relay affect the local workflow.
            _ = RelayAsync(e.Message);
        }

        private async Task RelayAsync(BandMessage msg)
        {
            try
            {
                var client = _httpFactory.CreateClient("band");
                client.BaseAddress = new Uri(_options.ApiBaseUrl);
                client.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _options.ApiKey);

                var payload = new
                {
                    workspace = _options.Workspace,
                    room = msg.RoomId,
                    agent = msg.SenderId,
                    agentName = msg.SenderName,
                    role = msg.SenderKind.ToString(),
                    type = msg.Kind.ToString(),
                    text = msg.Text,
                    handoffTo = msg.HandoffTo,
                    data = msg.Data,
                    sentAt = msg.CreatedAt
                };

                using var resp = await client.PostAsJsonAsync(
                    $"/v1/workspaces/{_options.Workspace}/rooms/{msg.RoomId}/messages", payload);

                if (!resp.IsSuccessStatusCode)
                {
                    _logger.LogDebug("Band live relay returned {Status} for {Kind}", resp.StatusCode, msg.Kind);
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Band live relay skipped (offline or unreachable)");
            }
        }
    }
}
