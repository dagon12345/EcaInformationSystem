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

        // Suggests the next version by bumping the patch segment of the latest
        // published version (e.g. "1.4.0" -> "1.4.1"). Falls back to "1.0.0"
        // when nothing has been published yet or the latest version isn't in
        // a recognizable Major.Minor.Patch form — the caller can always type
        // over the suggestion, this is just a convenience default.
        public async Task<string> GetNextVersionAsync()
        {
            var latest = await _repository.GetLatestAsync();
            if (latest is null)
                return "1.0.0";

            var parts = latest.Version.Trim().Split('.');
            if (parts.Length == 3
                && int.TryParse(parts[0], out var major)
                && int.TryParse(parts[1], out var minor)
                && int.TryParse(parts[2], out var patch))
            {
                return $"{major}.{minor}.{patch + 1}";
            }

            return latest.Version;
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
