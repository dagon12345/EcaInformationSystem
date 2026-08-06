using EcaInformationSystem.Api.Hubs;
using EcaInformationSystem.Application.Interfaces;
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

        public TransactionTierHubBroadcaster(IHubContext<TransactionTierHub> hub)
        {
            _hub = hub;
        }

        public async Task NotifyTransactionRecordedAsync(string userName)
        {
            await _hub.Clients.All.SendAsync("TransactionRecorded", userName);
        }
    }
}
