using EcaInformationSystem.Shared.DTOs;

namespace EcaInformationSystem.Application.Interfaces.Services
{
    public interface IStickyNoteService
    {
        Task<List<StickyNoteDto>> GetMineAsync(Guid userId);
        Task<StickyNoteDto> UpsertAsync(Guid userId, StickyNoteUpsertDto dto);
        Task DeleteAsync(Guid userId, Guid noteId);
    }
}
