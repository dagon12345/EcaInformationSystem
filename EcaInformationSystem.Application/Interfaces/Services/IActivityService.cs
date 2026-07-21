using EcaInformationSystem.Shared.DTOs.Activity;

namespace EcaInformationSystem.Application.Interfaces.Services
{
    public interface IActivityService
    {
        Task<ActivityDto?> GetByIdAsync(int id);
        Task<ActivityDto> CreateAsync(ActivityUpsertDto dto, string userId, int regionCode);
        Task<ActivityDto?> UpdateAsync(ActivityUpsertDto dto, string userId);
        Task<bool> DeleteAsync(int id);
        Task<List<ActivityMonthMarkerDto>> GetMonthMarkersAsync(int year, int month, string? provinceCode, bool publicOnly = false);
        Task<List<ActivityDto>> GetActivitiesForDateAsync(DateTime date, string? provinceCode, bool publicOnly = false);
        Task<List<ActivityDto>> GetPublicUpcomingAsync(int take = 8);
    }
}