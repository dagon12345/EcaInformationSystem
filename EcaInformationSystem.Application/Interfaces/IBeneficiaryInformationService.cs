

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
        Task<CreateBeneficiaryResultDto> CreateAsync(CreateBeneficiaryInformationDto dto, string userName);
        Task UpdateAsync(Guid Id, BeneficiaryInformationDto dto, string userName);
        Task<IEnumerable<LogSummaryResultDto>> GetLogSummaryAsync(Guid beneficiaryId);
        Task<PagedResultDto<BeneficiaryInformationDto>> GetPaginatedAsync(BeneficiaryFilterDto filter);
        Task<List<string>> GetExcelSheetNamesAsync(Stream fileStream, string fileName);
        Task<byte[]> ExportFilteredAsTemplateAsync(BeneficiaryFilterDto filter);
        Task<BeneficiaryImportResultDto> UpdateExcelAsync(Stream fileStream, string fileName, string sheetName, string userName);
        Task BulkUpdatePaymentStatusAsync(List<Guid> ids, int paymentStatus, int? modeOfPayment, DateTime? paymentDate, string userName, Dictionary<Guid, byte[]>? rowVersions = null);
        byte[] GenerateImportTemplate();
        Task<BeneficiaryInformationDto?> GetByIdAsync(Guid id);
        Task<List<BeneficiaryInformationDto>> GetByIdsAsync(List<Guid> ids);
        Task<byte[]> GeneratePayrollAsync(PayrollSettingsDto settings);
        Task<byte[]> GenerateCdrAsync(LiquidationFilterDto filter, LiquidationSettingsDto settings);
        Task<List<LiquidationPreviewRowDto>> BuildCdrPreviewAsync(LiquidationFilterDto filter, LiquidationSettingsDto settings);
        Task<BeneficiaryPreviewResultDto> PreviewImportAsync(Stream fileStream, string fileName, string sheetName);
        Task<BeneficiaryImportResultDto> ConfirmImportAsync(
                Stream fileStream,
                string fileName,
                string sheetName,
                string userName,
                HashSet<int> skipRows,
                int? quarter,      // ✅ new
                string? batch,     // ✅ new
                int? refYear);     // ✅ new
        Task BulkUpdateEligibilityAndBatchCodeAsync(List<Guid> ids, bool? isEligible, string? batchCode, string userName, Dictionary<Guid, byte[]>? rowVersions = null);
        Task BulkUpdateCoStatusAsync(List<Guid> ids, int? coStatus, DateTime? coDateEndorsed, DateTime? coDateApproved, string userName, Dictionary<Guid, byte[]>? rowVersions = null);
        Task BulkAssignRefNumberAsync(List<Guid> ids, int quarter, string batch, int refYear, string userName);
        Task<PossibleDuplicateSummaryDto> GetPossibleDuplicatesAsync(BeneficiaryFilterDto filter);
    }
}
