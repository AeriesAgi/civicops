using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace CivicOps.Band
{
    public class BandScenario
    {
        public string Key { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Channel { get; set; } = "Web";
        public string Area { get; set; } = string.Empty;
        public string RawText { get; set; } = string.Empty;
    }

    /// <summary>
    /// Drives a complete multi-agent dispatch end to end for demos and the video:
    /// a serious report enters, all five agents coordinate through Band, the
    /// human confirmation step is exercised (auto or manual), the incident is
    /// monitored and resolved, and the Band room is summarised.
    /// </summary>
    public class BandSimulationService
    {
        private readonly BandAgentService _band;
        private readonly BandOptions _options;
        private readonly ILogger<BandSimulationService> _logger;

        public static readonly IReadOnlyList<BandScenario> Scenarios = new List<BandScenario>
        {
            new()
            {
                Key = "water-main-leak",
                Title = "Burst water main threatening homes",
                Channel = "WhatsApp",
                Area = "Phoenix",
                RawText = "Voice note transcript: There is a burst water pipe outside 14 Palmview Road in Phoenix. Water is flooding the road and running toward houses, the pavement is breaking up and cars are swerving. Please send water and road crews urgently. My name is Aisha, phone 555-0108."
            },
            new()
            {
                Key = "structure-fire",
                Title = "Structural fire with people trapped",
                Channel = "WhatsApp",
                Area = "Chatsworth",
                RawText = "Theres a big fire at a block of flats in Chatsworth, smoke everywhere and people are trapped on the top floor screaming for help. Life threatening, please send help fast!"
            },
            new()
            {
                Key = "mva-injuries",
                Title = "Multi-vehicle accident with injuries",
                Channel = "VoiceNote",
                Area = "Durban CBD",
                RawText = "Bad car accident on the M4 near Durban CBD, two cars and a taxi, people are hurt and bleeding, someone is trapped in a car. Medical emergency."
            },
            new()
            {
                Key = "armed-robbery",
                Title = "Armed robbery in progress",
                Channel = "Android",
                Area = "Umlazi",
                RawText = "Armed robbery happening right now at the shop on the corner in Umlazi, men with guns, danger to life, we need armed response immediately."
            },
            new()
            {
                Key = "flooding",
                Title = "Flash flooding cutting off residents",
                Channel = "Web",
                Area = "Isipingo",
                RawText = "Heavy flooding in Isipingo, the river burst its banks, water is rising fast and families are trapped in their houses, road is washed away."
            }
        };

        public BandSimulationService(
            BandAgentService band,
            BandOptions options,
            ILogger<BandSimulationService> logger)
        {
            _band = band;
            _options = options;
            _logger = logger;
        }

        /// <summary>Starts a scenario and (optionally) auto-drives the human steps.
        /// Returns the room id immediately so the UI can open the live viewer.</summary>
        public string Run(string? scenarioKey, bool autoConfirm)
        {
            var scenario = Scenarios.FirstOrDefault(s => s.Key == scenarioKey) ?? Scenarios[0];
            var roomId = _band.StartIncident(scenario.RawText, scenario.Area, scenario.Channel);
            _logger.LogInformation("Band simulation '{Key}' started in room {Room} (autoConfirm={Auto})",
                scenario.Key, roomId, autoConfirm);

            if (autoConfirm)
            {
                _ = Task.Run(() => AutoDriveAsync(roomId));
            }
            return roomId;
        }

        /// <summary>Watches the room and performs the human dispatcher + supervisor
        /// actions automatically, so the whole flow plays unattended for a video.</summary>
        private async Task AutoDriveAsync(string roomId)
        {
            try
            {
                var confirmed = false;
                var acked = false;
                var deadline = DateTime.UtcNow.AddSeconds(120);

                while (DateTime.UtcNow < deadline)
                {
                    await Task.Delay(600);
                    var view = _band.GetRoomView(roomId);
                    if (view is null) continue;

                    // Confirm the proposed dispatch as soon as the agent asks.
                    if (!confirmed && view.Room.AwaitingHumanConfirmation)
                    {
                        var proposal = view.Messages.LastOrDefault(m => m.Kind == BandMessageKind.AssignmentProposed);
                        var unitId = proposal is not null && proposal.Data.TryGetValue("recommendedUnitId", out var v)
                            ? v?.ToString() : null;

                        // brief pause to mimic a human reading the Band context
                        await Task.Delay(TimeSpan.FromSeconds(Math.Clamp(_options.TickSeconds, 0.5, 4)));
                        _band.SubmitHumanDecision(roomId, "confirm", unitId,
                            "Confirmed from Band room context — unit and ETA look correct.",
                            "Dispatcher (auto-demo)");
                        confirmed = true;
                    }

                    // Acknowledge any supervisor escalation.
                    if (!acked && view.Messages.Any(m => m.Kind == BandMessageKind.Escalation))
                    {
                        _band.PostSupervisorAck(roomId, "Backup unit authorised; maintaining oversight until resolved.");
                        acked = true;
                    }

                    if (view.Room.IsClosed) break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Band auto-drive failed for room {Room}", roomId);
            }
        }
    }
}
