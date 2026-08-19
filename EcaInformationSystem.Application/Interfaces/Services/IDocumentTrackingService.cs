using EcaInformationSystem.Shared.DTOs.Common;
using EcaInformationSystem.Shared.DTOs.DocumentTracking;

namespace EcaInformationSystem.Application.Interfaces.Services
{
    public interface IDocumentTrackingService
    {
        Task<PagedResultDto<TrackedDocumentListItemDto>> GetPagedAsync(int page, int pageSize, string? search);
        Task<TrackedDocumentDto?> GetByIdAsync(Guid id);
        Task<List<UserLookupDto>> GetTaggableUsersAsync();

        Task<TrackedDocumentDto> CreateAsync(CreateTrackedDocumentDto dto, Guid callerId, string callerName, string? callerRole);

        Task<TrackedDocumentDto> AcceptAsync(Guid docId, Guid callerId, string callerName);

        Task<TrackedDocumentDto> RelayAsync(Guid docId, RelayDocumentToDto dto, Guid callerId, string callerName, string? callerRole);
        Task<TrackedDocumentDto> ReturnAsync(Guid docId, ReturnDocumentDto dto, Guid callerId, string callerName, string? callerRole);
        Task<TrackedDocumentDto> CompleteAsync(Guid docId, CompleteDocumentDto dto, Guid callerId, string callerName);

        Task<TrackedDocumentDto> UpdateAsync(Guid docId, UpdateTrackedDocumentDto dto, Guid callerId, bool isSuperAdmin);
        Task DeleteAsync(Guid docId, string callerRole);

        Task<TrackedDocumentDto> UpdateRouteAsync(Guid docId, Guid routeId, UpdateDocumentRouteNoteDto dto, Guid callerId, bool isSuperAdmin);
    }
}
