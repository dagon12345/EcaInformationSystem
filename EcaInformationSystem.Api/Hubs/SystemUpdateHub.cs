using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace EcaInformationSystem.Api.Hubs
{
    [Authorize]
    public class SystemUpdateHub : Hub
    {
    }
}
