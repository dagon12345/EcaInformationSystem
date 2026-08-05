using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace EcaInformationSystem.Api.Hubs
{
    // Push-only — broadcasts biometric device connection status live so
    // Attendance Management and My DTR don't need to poll. Any authenticated
    // user can connect (My DTR needs the "please connect to office WiFi and
    // run the sync tool" prompt too), not just SuperAdmin/Finance — the
    // payload itself stays limited to non-sensitive fields (see
    // ZkDirectPollingService/SyncFreshnessDto) for anyone below admin roles.
    //
    // Joins a per-region group (same pattern as ActivityHub) so a sync cycle
    // in one region only pushes to that region's viewers — otherwise every
    // region would see every other region's device status flash across
    // their screen.
    [Authorize]
    public class BiometricStatusHub : Hub
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
