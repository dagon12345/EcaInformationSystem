using EcaInformationSystem.Shared.DTOs;

namespace EcaInformationSystem.Application.Interfaces.Services
{
    public interface IDirectoryService
    {
        // Online status is left false here and filled in by the caller (the API
        // controller), since presence tracking (VoiceCallTracker) lives in the
        // Api project's Hubs — Application doesn't depend on SignalR internals.
        Task<List<ProvinceDirectoryGroupDto>> GetProvincialDirectoryAsync();
    }
}
