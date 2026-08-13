using EcaInformationSystem.Application.Interfaces;
using EcaInformationSystem.Shared.DTOs;

namespace EcaInformationSystem.Infrastructure.Services
{
    // Default registration — overridden by Api's real SignalR broadcaster via a
    // later AddScoped<ITransactionTierBroadcaster,...> call in Program.cs (last
    // registration wins). Keeps Infrastructure buildable/runnable without Api.
    public class NoOpTransactionTierBroadcaster : ITransactionTierBroadcaster
    {
        public Task NotifyTransactionRecordedAsync(string userName) => Task.CompletedTask;
        public Task NotifyLeaderboardResetAsync(LeaderboardResetResultDto result) => Task.CompletedTask;
    }
}
