using EcaInformationSystem.Application.DTOs;
using EcaInformationSystem.Domain.Entities;
using Microsoft.AspNetCore.Http;

namespace EcaInformationSystem.Application.Interfaces
{
    public interface IBeneficiaryInformationService
    {
        Task<IEnumerable<BeneficiaryInformationDto>> GetAllAsync();
        Task<IEnumerable<BeneficiaryInformationDto>> FilterAsync(BeneficiaryFilterDto filter);
        Task<BeneficiarySummaryResultDto> GetSummaryAsync(BeneficiaryFilterDto filter);
        Task SoftDeleteAsync(Guid Id, string userName);
        Task<BeneficiaryInformationDto> CreateAsync(CreateBeneficiaryInformationDto dto, string userName);
        Task UpdateAsync(Guid Id, BeneficiaryInformationDto dto, string userName);
        Task<BeneficiaryImportResultDto> ImportExcelAsync(Stream fileStream, string fileName, string sheetName, string userName);
        Task<IEnumerable<LogSummaryResultDto>> GetLogSummaryAsync(Guid beneficiaryId);
        Task<PagedResultDto<BeneficiaryInformationDto>> GetPaginatedAsync(BeneficiaryFilterDto filter);
        Task<List<string>> GetExcelSheetNamesAsync(Stream fileStream, string fileName);
        Task<byte[]> ExportFilteredAsTemplateAsync(BeneficiaryFilterDto filter);
    }
}
