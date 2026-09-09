using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace EcaInformationSystem.Api.Hubs
{
    // Push-only — no client-invokable methods. DtrPunchRequestService pushes
    // "DtrPunchRequestSubmitted" to Clients.Users(targetUserIds) whenever a
    // non-Admin/Finance/SuperAdmin user files a self-service punch-edit
    // request. Same ChatUserIdProvider wiring as LivenessNotificationHub.
    [Authorize]
    public class DtrPunchRequestHub : Hub
    {
    }
}
