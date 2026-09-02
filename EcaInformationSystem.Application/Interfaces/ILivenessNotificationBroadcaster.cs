using EcaInformationSystem.Shared.DTOs;

namespace EcaInformationSystem.Application.Interfaces
{
    // Abstraction so LivenessCheckService can announce "a grantee just
    // submitted a liveness photo" without depending on SignalR/ASP.NET Core
    // hosting types — the Api layer supplies the real (SignalR) implementation
    // via DI; Infrastructure registers a no-op default so it still compiles/runs
    // standalone without the Api layer wired up.
    public interface ILivenessNotificationBroadcaster
    {
        Task NotifyLivenessSubmittedAsync(List<Guid> targetUserIds, LivenessSubmittedNotificationDto notification);
    }
}
