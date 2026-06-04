using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace CivicOps.Band.Agents
{
    /// <summary>
    /// Base class for a Band-resident agent. Every agent owns a Band client bound
    /// to its own identity, connects to the interaction layer, and reacts to the
    /// shared message stream. Reactions are dispatched off the publishing thread
    /// so an agent never blocks the room while it thinks, and never re-enters the
    /// broker synchronously.
    /// </summary>
    public abstract class BandAgent
    {
        protected readonly BandAgentClient Client;
        protected readonly IBandTransport Band;
        protected readonly ILogger Logger;

        public BandIdentity Identity => Client.Identity;

        protected BandAgent(IBandTransport transport, BandIdentity identity, ILogger logger)
        {
            Band = transport;
            Logger = logger;
            Client = new BandAgentClient(transport, identity);
            Client.Connect();
            Client.MessageReceived += OnMessage;
        }

        private void OnMessage(BandMessage message)
        {
            if (!ShouldHandle(message)) return;

            // Offload so the agent's reasoning never blocks the poster.
            _ = Task.Run(async () =>
            {
                try
                {
                    await HandleAsync(message);
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "{Agent} failed handling {Kind} in room {Room}",
                        Identity.DisplayName, message.Kind, message.RoomId);
                    Band.Post(message.RoomId, Identity, BandMessageKind.System,
                        $"⚠️ {Identity.DisplayName} hit an error handling {message.Kind}: {ex.Message}");
                }
            });
        }

        /// <summary>Fast, synchronous filter deciding whether this message is ours.</summary>
        protected abstract bool ShouldHandle(BandMessage message);

        /// <summary>The agent's actual work, run off-thread.</summary>
        protected abstract Task HandleAsync(BandMessage message);

        protected void JoinAndAnnounce(string roomId)
        {
            Client.Join(roomId);
        }

        protected static double GetDouble(BandMessage m, string key, double fallback = 0)
        {
            if (m.Data.TryGetValue(key, out var v) && v is not null)
            {
                switch (v)
                {
                    case double d: return d;
                    case int i: return i;
                    case long l: return l;
                    case float f: return f;
                }
                if (double.TryParse(v.ToString(), System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out var p))
                    return p;
            }
            return fallback;
        }

        protected static string GetString(BandMessage m, string key, string fallback = "")
            => m.Data.TryGetValue(key, out var v) && v is not null ? v.ToString() ?? fallback : fallback;
    }
}
