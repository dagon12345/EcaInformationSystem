using EcaInformationSystem.Shared.DTOs.Dtr;

namespace EcaInformationSystem.Application.Interfaces
{
    // Abstraction so DtrPunchRequestService can announce "a new punch-edit
    // request needs review" without depending on SignalR/ASP.NET Core hosting
    // types — same reasoning as ILivenessNotificationBroadcaster. The Api
    // layer supplies the real (SignalR) implementation via DI; Infrastructure
    // registers a no-op default so it still compiles/runs standalone.
    public interface IDtrPunchRequestBroadcaster
    {
        Task NotifyPunchRequestSubmittedAsync(List<Guid> targetUserIds, DtrPunchRequestSubmittedNotificationDto notification);
    }
}
