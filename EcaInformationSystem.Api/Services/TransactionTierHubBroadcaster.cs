using EcaInformationSystem.Api.Hubs;
using EcaInformationSystem.Application.Interfaces;
using EcaInformationSystem.Shared.DTOs;
using Microsoft.AspNetCore.SignalR;

namespace EcaInformationSystem.Api.Services
{
    // Real implementation of the broadcaster abstraction LogRepository depends
    // on — lives in Api because that's the only layer allowed to know about
    // SignalR hub types/hosting. Registered in Program.cs AFTER AddInfrastructure()
    // so it overrides Infrastructure's no-op default registration.
    public class TransactionTierHubBroadcaster : ITransactionTierBroadcaster
    {
        private readonly IHubContext<TransactionTierHub> _hub;
        private readonly IHubContext<PostsHub> _postsHub;

        public TransactionTierHubBroadcaster(IHubContext<TransactionTierHub> hub, IHubContext<PostsHub> postsHub)
        {
            _hub = hub;
            _postsHub = postsHub;
        }

        public async Task NotifyTransactionRecordedAsync(string userName)
        {
            await _hub.Clients.All.SendAsync("TransactionRecorded", userName);
        }

        public async Task NotifyLeaderboardResetAsync(LeaderboardResetResultDto result)
        {
            await _hub.Clients.All.SendAsync("LeaderboardReset", result);

            // Both reset call sites (the weekly auto-reset job and the SuperAdmin
            // manual-reset endpoint) funnel through here, so broadcasting the
            // podium announcement post from this one place covers both without
            // duplicating it at each call site. Same event name PostsController
            // uses for a normal new post, so PostFeed.razor needs no extra wiring.
            if (result.PodiumPost is not null)
                await _postsHub.Clients.All.SendAsync("PostCreated", result.PodiumPost);
        }
    }
}
