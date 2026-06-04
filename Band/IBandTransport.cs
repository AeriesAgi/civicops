using System;
using System.Collections.Generic;

namespace CivicOps.Band
{
    /// <summary>
    /// The Band interaction layer, abstracted. Both the in-process
    /// <see cref="LocalBandBroker"/> (simulation / offline demo) and a future
    /// live band.ai transport implement this identical contract, so the three
    /// agents are written once and coordinate the same way regardless of where
    /// Band physically runs.
    /// </summary>
    public interface IBandTransport
    {
        /// <summary>Raised for every message posted to any room — this is how
        /// agents and the UI observe the shared conversation.</summary>
        event EventHandler<BandMessageEventArgs>? MessagePosted;

        BandRoom CreateRoom(string roomId, string title, string incidentReference, string area);
        BandRoom? GetRoom(string roomId);
        IReadOnlyList<BandRoom> ListRooms();

        void Connect(BandIdentity identity);
        void Join(string roomId, BandIdentity identity);

        BandMessage Post(
            string roomId,
            BandIdentity sender,
            BandMessageKind kind,
            string text,
            Dictionary<string, object?>? data = null,
            string? handoffTo = null);

        IReadOnlyList<BandMessage> GetMessages(string roomId);

        void UpdateRoom(string roomId, Action<BandRoom> mutate);
    }
}
