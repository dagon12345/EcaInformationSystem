using EcaInformationSystem.Shared.DTOs.Dtr;

namespace EcaInformationSystem.Application.Interfaces.Services
{
    public interface IDtrService
    {
        Task<DtrDocumentDto?> GetForUserAsync(Guid userId, DateTime periodStart, DateTime periodEnd);
        Task<List<DtrSummaryDto>> GetAllSummariesAsync(DateTime periodStart, DateTime periodEnd, int regionCode);
    }
}
