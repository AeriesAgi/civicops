using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace CivicOps.Band
{
    /// <summary>
    /// In-process implementation of the Band interaction layer. This is a faithful
    /// model of how Band works as a product: identities connect, rooms hold shared
    /// context, every participant publishes messages and subscribes to the stream,
    /// and the full ordered history is retained as an audit trail.
    ///
    /// Running it in-process means the multi-agent demo works with zero external
    /// dependencies (no network, no keys), while the <see cref="IBandTransport"/>
    /// seam lets the exact same agents target a hosted band.ai room in production.
    /// </summary>
    public class LocalBandBroker : IBandTransport
    {
        private readonly ConcurrentDictionary<string, BandRoom> _rooms = new();
        private readonly ConcurrentDictionary<string, List<BandMessage>> _messages = new();
        private readonly ConcurrentDictionary<string, BandIdentity> _identities = new();
        private readonly ILogger<LocalBandBroker> _logger;
        private readonly object _seqLock = new();
        private int _sequence;

        public event EventHandler<BandMessageEventArgs>? MessagePosted;

        public LocalBandBroker(ILogger<LocalBandBroker> logger)
        {
            _logger = logger;
        }

        public void Connect(BandIdentity identity)
        {
            _identities[identity.Id] = identity;
            _logger.LogInformation("Band identity connected: {Identity} ({Role})", identity.DisplayName, identity.Role);
        }

        public BandRoom CreateRoom(string roomId, string title, string incidentReference, string area)
        {
            var room = _rooms.GetOrAdd(roomId, _ => new BandRoom
            {
                Id = roomId,
                Title = title,
                IncidentReference = incidentReference,
                Area = area,
                CreatedAt = DateTime.UtcNow,
                LastActivityAt = DateTime.UtcNow
            });
            _messages.GetOrAdd(roomId, _ => new List<BandMessage>());
            return room;
        }

        public BandRoom? GetRoom(string roomId) => _rooms.TryGetValue(roomId, out var r) ? r : null;

        public IReadOnlyList<BandRoom> ListRooms() =>
            _rooms.Values.OrderByDescending(r => r.LastActivityAt).ToList();

        public void Join(string roomId, BandIdentity identity)
        {
            _identities[identity.Id] = identity;
            if (_rooms.TryGetValue(roomId, out var room))
            {
                lock (room.Members)
                {
                    if (!room.Members.Contains(identity.DisplayName))
                    {
                        room.Members.Add(identity.DisplayName);
                    }
                }
            }
        }

        public BandMessage Post(
            string roomId,
            BandIdentity sender,
            BandMessageKind kind,
            string text,
            Dictionary<string, object?>? data = null,
            string? handoffTo = null)
        {
            if (!_messages.TryGetValue(roomId, out var log))
            {
                log = _messages.GetOrAdd(roomId, _ => new List<BandMessage>());
            }

            int seq;
            lock (_seqLock) { seq = ++_sequence; }

            var message = new BandMessage
            {
                RoomId = roomId,
                SenderId = sender.Id,
                SenderName = sender.DisplayName,
                SenderKind = sender.Kind,
                SenderColor = sender.Color,
                SenderAvatar = sender.Avatar,
                Kind = kind,
                Text = text,
                Data = data ?? new Dictionary<string, object?>(),
                HandoffTo = handoffTo,
                Sequence = seq,
                CreatedAt = DateTime.UtcNow
            };

            lock (log) { log.Add(message); }

            if (_rooms.TryGetValue(roomId, out var room))
            {
                room.LastActivityAt = DateTime.UtcNow;
            }

            _logger.LogInformation("[Band:{Room}] {Sender} -> {Kind}: {Text}",
                roomId.Length > 8 ? roomId[..8] : roomId, sender.DisplayName, kind, Truncate(text, 80));

            // Fan out to every subscriber: agents react, the UI renders live.
            MessagePosted?.Invoke(this, new BandMessageEventArgs(message));
            return message;
        }

        public IReadOnlyList<BandMessage> GetMessages(string roomId) =>
            _messages.TryGetValue(roomId, out var log)
                ? log.OrderBy(m => m.Sequence).ToList()
                : new List<BandMessage>();

        public void UpdateRoom(string roomId, Action<BandRoom> mutate)
        {
            if (_rooms.TryGetValue(roomId, out var room))
            {
                mutate(room);
                room.LastActivityAt = DateTime.UtcNow;
            }
        }

        private static string Truncate(string value, int max) =>
            string.IsNullOrEmpty(value) || value.Length <= max ? value : value[..max] + "…";
    }
}
