using EcaInformationSystem.Application.Interfaces;
using EcaInformationSystem.Shared.DTOs;

namespace EcaInformationSystem.Infrastructure.Services
{
    // Default registration — overridden by Api's real SignalR broadcaster via a
    // later AddScoped<ILivenessNotificationBroadcaster,...> call in Program.cs
    // (last registration wins). Keeps Infrastructure buildable/runnable without Api.
    public class NoOpLivenessNotificationBroadcaster : ILivenessNotificationBroadcaster
    {
        public Task NotifyLivenessSubmittedAsync(List<Guid> targetUserIds, LivenessSubmittedNotificationDto notification)
            => Task.CompletedTask;
    }
}
