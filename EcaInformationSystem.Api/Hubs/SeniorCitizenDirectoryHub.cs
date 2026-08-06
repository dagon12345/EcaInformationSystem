using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace EcaInformationSystem.Api.Hubs
{
    [Authorize]
    public class SeniorCitizenDirectoryHub : Hub
    {
        // Every connection joins a region-scoped group so broadcasts never
        // cross regions — this is a multi-region system, and a live "entry
        // changed" push must be as region-isolated as the REST API already is.
        public override async Task OnConnectedAsync()
        {
            var region = Context.User?.FindFirst("Region")?.Value;
            if (!string.IsNullOrWhiteSpace(region))
                await Groups.AddToGroupAsync(Context.ConnectionId, RegionGroupName(region));

            await base.OnConnectedAsync();
        }

        public static string RegionGroupName(string regionCode) => $"senior-citizen-directory-region-{regionCode}";
    }
}
