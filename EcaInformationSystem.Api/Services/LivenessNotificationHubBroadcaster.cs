using EcaInformationSystem.Api.Hubs;
using EcaInformationSystem.Application.Interfaces;
using EcaInformationSystem.Shared.DTOs;
using Microsoft.AspNetCore.SignalR;

namespace EcaInformationSystem.Api.Services
{
    // Real implementation of the broadcaster abstraction LivenessCheckService
    // depends on — lives in Api because that's the only layer allowed to know
    // about SignalR hub types/hosting. Registered in Program.cs AFTER
    // AddInfrastructure() so it overrides Infrastructure's no-op default.
    public class LivenessNotificationHubBroadcaster : ILivenessNotificationBroadcaster
    {
        private readonly IHubContext<LivenessNotificationHub> _hub;

        public LivenessNotificationHubBroadcaster(IHubContext<LivenessNotificationHub> hub)
        {
            _hub = hub;
        }

        public async Task NotifyLivenessSubmittedAsync(List<Guid> targetUserIds, LivenessSubmittedNotificationDto notification)
        {
            if (targetUserIds.Count == 0) return;

            await _hub.Clients.Users(targetUserIds.Select(id => id.ToString()).ToList())
                .SendAsync("LivenessSubmitted", notification);
        }
    }
}
