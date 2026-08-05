using EcaInformationSystem.Shared.DTOs.Dtr;

namespace EcaInformationSystem.Application.Interfaces.Services
{
    public interface IDtrDayMarkService
    {
        Task<List<DtrDayMarkDto>> GetForUserAsync(Guid userId, DateTime start, DateTime end);
        Task SetAsync(Guid userId, DateTime date, string markType, string? noteText, string? updatedByName);
        Task ClearAsync(Guid userId, DateTime date);
    }
}
