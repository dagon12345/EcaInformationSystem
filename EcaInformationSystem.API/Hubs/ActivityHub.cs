// Api/Hubs/ActivityHub.cs
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Authorization;

namespace EcaInformationSystem.Api.Hubs
{
    [Authorize]
    public class ActivityHub : Hub
    {
        // No client-invoked methods needed — this hub only pushes server → client.
    }
}