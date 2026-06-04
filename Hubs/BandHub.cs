using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;

namespace CivicOps.Hubs
{
    /// <summary>
    /// SignalR hub that streams Band room activity to the browser, so the Band
    /// Room Viewer renders agent messages and hand-offs the instant they happen.
    /// </summary>
    public class BandHub : Hub
    {
        public const string ConsoleGroup = "band-console";

        public Task JoinRoom(string roomId) =>
            Groups.AddToGroupAsync(Context.ConnectionId, roomId);

        public Task LeaveRoom(string roomId) =>
            Groups.RemoveFromGroupAsync(Context.ConnectionId, roomId);

        public Task JoinConsole() =>
            Groups.AddToGroupAsync(Context.ConnectionId, ConsoleGroup);
    }
}
