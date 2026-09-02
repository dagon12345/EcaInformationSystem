using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace EcaInformationSystem.Api.Hubs
{
    // Push-only — no client-invokable methods. LivenessCheckService pushes
    // "LivenessSubmitted" to Clients.Users(targetUserIds) whenever a grantee
    // successfully submits their liveness photo. ChatUserIdProvider (registered
    // globally in Program.cs) already maps every hub connection to the app's
    // user id via the JWT "sub" claim, so Clients.Users(...) works here without
    // any extra wiring.
    [Authorize]
    public class LivenessNotificationHub : Hub
    {
    }
}
