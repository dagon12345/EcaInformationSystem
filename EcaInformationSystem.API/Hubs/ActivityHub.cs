// Api/Hubs/ActivityHub.cs
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Authorization;

namespace EcaInformationSystem.Api.Hubs
{
    [Authorize]
    public class ActivityHub : Hub
    {
         public override async Task OnConnectedAsync()
        {
            var regionCode = Context.User?.FindFirst("Region")?.Value;
            if (!string.IsNullOrEmpty(regionCode))
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, $"region-{regionCode}");
            }
            await base.OnConnectedAsync();
        }
    }
}