using EcaInformationSystem.Shared.DTOs.SystemUpdate;

namespace EcaInformationSystem.Application.Interfaces.Services
{
    public interface ISystemUpdateNoticeService
    {
        Task<List<SystemUpdateNoticeDto>> GetAllAsync();
        Task<SystemUpdateNoticeDto?> GetLatestAsync();
        Task<string> GetNextVersionAsync();
        Task<SystemUpdateNoticeDto> PublishAsync(CreateSystemUpdateNoticeDto dto, Guid callerId, string callerName);

        // Editable .docx summary of every published release note, each stamped
        // with the date/time it was published.
        Task<(byte[] Bytes, string FileName)> GetSummaryAsWordAsync(string requestedBy);
    }
}
