using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace ScoutsAttendance.API.Hubs;

[Authorize]
public class AttendanceHub : Hub
{
    /// <summary>Client calls this to subscribe to real-time updates for a specific event.</summary>
    public async Task JoinEvent(string eventId) =>
        await Groups.AddToGroupAsync(Context.ConnectionId, $"event-{eventId}");

    /// <summary>Client calls this when leaving an event's attendance page.</summary>
    public async Task LeaveEvent(string eventId) =>
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"event-{eventId}");
}
