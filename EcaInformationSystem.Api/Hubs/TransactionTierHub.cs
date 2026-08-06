using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace EcaInformationSystem.Api.Hubs
{
    // Pushes "a qualifying transaction was just recorded" to every connected
    // client so the leaderboard/tier badges stay live — system-wide (not
    // region-scoped), since the leaderboard itself ranks across all regions.
    [Authorize]
    public class TransactionTierHub : Hub
    {
    }
}
