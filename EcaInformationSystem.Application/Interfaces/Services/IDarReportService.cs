using EcaInformationSystem.Shared.DTOs.DailyAccomplishmentReport;

namespace EcaInformationSystem.Application.Interfaces.Services
{
    public interface IDarReportService
    {
        Task<List<DarReportListItemDto>> GetMineAsync(Guid userId);
        Task<DarReportDto?> GetByIdAsync(Guid id, Guid userId);
        Task<DarReportDto> UpsertAsync(Guid userId, DarReportUpsertDto dto);
        Task DeleteAsync(Guid userId, Guid id);
    }
}
