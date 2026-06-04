using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace CivicOps.Band
{
    /// <summary>
    /// Optional live mirror to the Band platform. When Band:Mode=Live, every
    /// message the agents publish locally is also best-effort relayed to the
    /// Node <c>band-bridge</c> sidecar, which republishes it to a hosted Band
    /// workspace using the official <c>@band-sdk/core</c> SDK. The local broker
    /// stays the source of truth, so the demo runs even with no network and no
    /// sidecar — this layer is purely additive.
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
                _logger.LogInformation("Band live mirror enabled → band-bridge {Url} (workspace {Ws})",
                    _options.BridgeUrl, _options.Workspace);
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
                client.BaseAddress = new Uri(_options.BridgeUrl);
                client.Timeout = TimeSpan.FromSeconds(4);

                var payload = new
                {
                    agentId = msg.SenderId,
                    agentName = msg.SenderName,
                    role = msg.SenderKind.ToString(),
                    type = msg.Kind.ToString(),
                    text = msg.Text,
                    handoffTo = msg.HandoffTo,
                    data = msg.Data,
                    sentAt = msg.CreatedAt
                };

                using var resp = await client.PostAsJsonAsync(
                    $"/rooms/{msg.RoomId}/messages", payload);

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
