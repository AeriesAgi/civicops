using System;
using CivicOps.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace CivicOps.Band
{
    /// <summary>
    /// Bridges the Band interaction layer to SignalR. Every message posted to any
    /// room is pushed live to the room's viewers and to the Band console, so the
    /// UI is a true real-time window onto agent coordination.
    /// </summary>
    public class BandRealtimeBroadcaster
    {
        private readonly IHubContext<BandHub> _hub;
        private readonly ILogger<BandRealtimeBroadcaster> _logger;

        public BandRealtimeBroadcaster(
            IBandTransport transport,
            IHubContext<BandHub> hub,
            ILogger<BandRealtimeBroadcaster> logger)
        {
            _hub = hub;
            _logger = logger;
            transport.MessagePosted += OnMessagePosted;
        }

        private void OnMessagePosted(object? sender, BandMessageEventArgs e)
        {
            var msg = e.Message;
            try
            {
                _hub.Clients.Group(msg.RoomId).SendAsync("ReceiveBandMessage", BandDto.Message(msg));
                _hub.Clients.Group(BandHub.ConsoleGroup).SendAsync("RoomActivity", new
                {
                    roomId = msg.RoomId,
                    kind = msg.Kind.ToString(),
                    sender = msg.SenderName,
                    text = msg.Text,
                    at = msg.CreatedAt
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to broadcast Band message to SignalR");
            }
        }
    }
}
