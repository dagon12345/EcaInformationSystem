using EcaInformationSystem.Api.Hubs;
using EcaInformationSystem.Application.Interfaces;
using EcaInformationSystem.Shared.DTOs.Dtr;
using Microsoft.AspNetCore.SignalR;

namespace EcaInformationSystem.Api.Services
{
    // Real implementation of the broadcaster abstraction DtrPunchRequestService
    // depends on — lives in Api because that's the only layer allowed to know
    // about SignalR hub types/hosting. Registered in Program.cs AFTER
    // AddInfrastructure() so it overrides Infrastructure's no-op default.
    public class DtrPunchRequestHubBroadcaster : IDtrPunchRequestBroadcaster
    {
        private readonly IHubContext<DtrPunchRequestHub> _hub;

        public DtrPunchRequestHubBroadcaster(IHubContext<DtrPunchRequestHub> hub)
        {
            _hub = hub;
        }

        public async Task NotifyPunchRequestSubmittedAsync(List<Guid> targetUserIds, DtrPunchRequestSubmittedNotificationDto notification)
        {
            if (targetUserIds.Count == 0) return;

            await _hub.Clients.Users(targetUserIds.Select(id => id.ToString()).ToList())
                .SendAsync("DtrPunchRequestSubmitted", notification);
        }
    }
}
