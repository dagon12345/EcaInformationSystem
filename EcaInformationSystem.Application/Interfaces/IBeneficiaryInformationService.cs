

using EcaInformationService.Shared.DTOs;
using EcaInformationSystem.Shared.DTOs;

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
        Task<IEnumerable<LogSummaryResultDto>> GetLogSummaryAsync(Guid beneficiaryId);
        Task<PagedResultDto<BeneficiaryInformationDto>> GetPaginatedAsync(BeneficiaryFilterDto filter);
        Task<List<string>> GetExcelSheetNamesAsync(Stream fileStream, string fileName);
        Task<byte[]> ExportFilteredAsTemplateAsync(BeneficiaryFilterDto filter);
        Task<BeneficiaryImportResultDto> UpdateExcelAsync(Stream fileStream, string fileName, string sheetName, string userName);
        Task BulkUpdatePaymentStatusAsync(List<Guid> ids, int paymentStatus, DateTime? paymentDate, string userName);
        byte[] GenerateImportTemplate();
        Task<BeneficiaryInformationDto?> GetByIdAsync(Guid id);
        Task<List<BeneficiaryInformationDto>> GetByIdsAsync(List<Guid> ids);
        Task<byte[]> GeneratePayrollAsync(PayrollSettingsDto settings);
        Task<byte[]> GenerateCdrAsync(LiquidationFilterDto filter, LiquidationSettingsDto settings);
        Task<List<LiquidationPreviewRowDto>> BuildCdrPreviewAsync(LiquidationFilterDto filter, LiquidationSettingsDto settings);
        Task<BeneficiaryPreviewResultDto> PreviewImportAsync(Stream fileStream, string fileName, string sheetName);
        Task<BeneficiaryImportResultDto> ConfirmImportAsync(Stream fileStream, string fileName, string sheetName, string userName, HashSet<int> skipRows);
        Task BulkUpdateEligibilityAndBatchCodeAsync(List<Guid> ids, bool? isEligible, string? batchCode, string userName);
    }
}
