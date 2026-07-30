using EcaInformationSystem.Shared.DTOs.SystemUpdate;

namespace EcaInformationSystem.Application.Interfaces.Services
{
    public interface ISystemUpdateNoticeService
    {
        Task<List<SystemUpdateNoticeDto>> GetAllAsync();
        Task<SystemUpdateNoticeDto?> GetLatestAsync();
        Task<SystemUpdateNoticeDto> PublishAsync(CreateSystemUpdateNoticeDto dto, Guid callerId, string callerName);
    }
}
