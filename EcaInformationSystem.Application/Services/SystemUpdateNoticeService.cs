using EcaInformationSystem.Application.Interfaces;
using EcaInformationSystem.Application.Interfaces.Services;
using EcaInformationSystem.Domain.Entities;
using EcaInformationSystem.Shared.DTOs.SystemUpdate;

namespace EcaInformationSystem.Application.Services
{
    public class SystemUpdateNoticeService : ISystemUpdateNoticeService
    {
        private readonly ISystemUpdateNoticeRepository _repository;

        public SystemUpdateNoticeService(ISystemUpdateNoticeRepository repository)
        {
            _repository = repository;
        }

        public async Task<List<SystemUpdateNoticeDto>> GetAllAsync()
        {
            var notices = await _repository.GetAllAsync();
            return notices.Select(ToDto).ToList();
        }

        public async Task<SystemUpdateNoticeDto?> GetLatestAsync()
        {
            var notice = await _repository.GetLatestAsync();
            return notice is null ? null : ToDto(notice);
        }

        public async Task<SystemUpdateNoticeDto> PublishAsync(CreateSystemUpdateNoticeDto dto, Guid callerId, string callerName)
        {
            var notice = new SystemUpdateNotice
            {
                Id = Guid.NewGuid(),
                Version = dto.Version.Trim(),
                Title = dto.Title.Trim(),
                Changes = dto.Changes.Trim(),
                PublishedAt = DateTime.UtcNow,
                PublishedByUserId = callerId,
                PublishedByName = callerName
            };

            await _repository.AddAsync(notice);
            return ToDto(notice);
        }

        private static SystemUpdateNoticeDto ToDto(SystemUpdateNotice notice) => new()
        {
            Id = notice.Id,
            Version = notice.Version,
            Title = notice.Title,
            Changes = notice.Changes,
            PublishedAt = notice.PublishedAt,
            PublishedByName = notice.PublishedByName
        };
    }
}
