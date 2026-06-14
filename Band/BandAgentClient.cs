using System;
using System.Collections.Generic;

namespace CivicOps.Band
{
    /// <summary>
    /// C# Band client wrapper bound to a single identity. This is the surface an
    /// agent (or a human connector) uses to participate in Band: connect, join a
    /// room, publish typed messages, and subscribe to everything other members
    /// say. It deliberately mirrors the ergonomics of the official Band SDK
    /// (<c>@band-ai/sdk</c>) client (connect, join a room, post a message,
    /// react to the stream) so the mental model matches the Band platform.
    /// </summary>
    public class BandAgentClient
    {
        private readonly IBandTransport _transport;

        public BandIdentity Identity { get; }

        /// <summary>Fires for messages authored by *other* members (an agent does
        /// not react to its own posts), in rooms this client cares about.</summary>
        public event Action<BandMessage>? MessageReceived;

        public BandAgentClient(IBandTransport transport, BandIdentity identity)
        {
            _transport = transport;
            Identity = identity;
            _transport.MessagePosted += OnTransportMessage;
        }

        public void Connect() => _transport.Connect(Identity);

        public void Join(string roomId) => _transport.Join(roomId, Identity);

        public BandMessage Post(
            string roomId,
            BandMessageKind kind,
            string text,
            Dictionary<string, object?>? data = null,
            string? handoffTo = null)
            => _transport.Post(roomId, Identity, kind, text, data, handoffTo);

        public IReadOnlyList<BandMessage> History(string roomId) => _transport.GetMessages(roomId);

        private void OnTransportMessage(object? sender, BandMessageEventArgs e)
        {
            // Ignore our own messages so agents never loop on themselves.
            if (e.Message.SenderId == Identity.Id) return;
            MessageReceived?.Invoke(e.Message);
        }
    }
}
