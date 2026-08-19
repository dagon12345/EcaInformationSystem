using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace EcaInformationSystem.Api.Hubs
{
    // Push-only — no client-invokable methods. ApplicationTrackingController pushes
    // "ApplicationTagged" to Clients.User(taggedUserId) whenever a batch's
    // CurrentHolderUserId changes to someone other than the caller.
    // ChatUserIdProvider (registered globally in Program.cs) already maps every
    // hub connection to the app's user id via the JWT "sub" claim, so
    // Clients.User(...) works here without any extra wiring.
    [Authorize]
    public class ApplicationTrackingHub : Hub
    {
    }
}
