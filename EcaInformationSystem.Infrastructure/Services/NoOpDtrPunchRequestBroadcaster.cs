using EcaInformationSystem.Application.Interfaces;
using EcaInformationSystem.Shared.DTOs.Dtr;

namespace EcaInformationSystem.Infrastructure.Services
{
    // Default registration — overridden by Api's real SignalR broadcaster via a
    // later AddScoped<IDtrPunchRequestBroadcaster,...> call in Program.cs
    // (last registration wins). Keeps Infrastructure buildable/runnable without Api.
    public class NoOpDtrPunchRequestBroadcaster : IDtrPunchRequestBroadcaster
    {
        public Task NotifyPunchRequestSubmittedAsync(List<Guid> targetUserIds, DtrPunchRequestSubmittedNotificationDto notification)
            => Task.CompletedTask;
    }
}
