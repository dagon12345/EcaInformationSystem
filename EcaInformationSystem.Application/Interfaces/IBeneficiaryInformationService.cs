

using EcaInformationSystem.Shared.DTOs;

namespace EcaInformationSystem.Application.Interfaces
{
    public interface IBeneficiaryInformationService
    {
        Task<IEnumerable<BeneficiaryInformationDto>> GetAllAsync();
        Task<PagedResultDto<BeneficiaryListItemDto>> GetPagedListAsync(BeneficiaryFilterDto filter);
        Task<int> GetMatchingCountAsync(BeneficiaryFilterDto filter);
        Task<IEnumerable<BeneficiaryInformationDto>> FilterAsync(BeneficiaryFilterDto filter);
        Task<BeneficiarySummaryResultDto> GetSummaryAsync(BeneficiaryFilterDto filter);
        Task SoftDeleteAsync(Guid Id, string userName);
        Task<CreateBeneficiaryResultDto> CreateAsync(CreateBeneficiaryInformationDto dto, string userName);
        Task UpdateAsync(Guid Id, BeneficiaryInformationDto dto, string userName);
        Task<IEnumerable<LogSummaryResultDto>> GetLogSummaryAsync(Guid beneficiaryId);
        Task<PagedResultDto<BeneficiaryInformationDto>> GetPaginatedAsync(BeneficiaryFilterDto filter);
        Task<List<string>> GetExcelSheetNamesAsync(Stream fileStream, string fileName);
        Task<byte[]> ExportFilteredAsTemplateAsync(BeneficiaryFilterDto filter, string userName);
        Task<BeneficiaryImportResultDto> UpdateExcelAsync(Stream fileStream, string fileName, string sheetName, string userName);
        byte[] GenerateImportTemplate();
        Task<BeneficiaryInformationDto?> GetByIdAsync(Guid id);
        Task<List<BeneficiaryInformationDto>> GetByIdsAsync(List<Guid> ids);
        Task<byte[]> GeneratePayrollAsync(PayrollSettingsDto settings, string userName);
        Task<byte[]> GenerateCdrAsync(LiquidationFilterDto filter, LiquidationSettingsDto settings);
        Task<List<LiquidationPreviewRowDto>> BuildCdrPreviewAsync(LiquidationFilterDto filter, LiquidationSettingsDto settings);
        Task<BeneficiaryPreviewResultDto> PreviewImportAsync(
            Stream fileStream, string fileName, string sheetName,
            Dictionary<int, Dictionary<string, string>>? corrections = null);
        byte[] ExportCrossmatchRowsAsTemplate(List<CrossmatchRowDto> rows, string sheetName);
        Task<CrossmatchResultDto> GetCrossmatchPreviewAsync(
            Stream fileStream, string fileName, string sheetName,
            Action<int, int>? onProgress = null, CancellationToken cancellationToken = default);
        Task<BeneficiaryImportResultDto> ConfirmImportAsync(
                Stream fileStream,
                string fileName,
                string sheetName,
                string userName,
                HashSet<int> skipRows,
                int? quarter,      // ✅ new
                string? batch,     // ✅ new
                int? refYear,      // ✅ new
                Dictionary<int, Dictionary<string, string>>? corrections = null);
        Task BulkUpdateEligibilityAndBatchCodeAsync(List<Guid> ids, bool? isEligible, string? batchCode, string userName, Dictionary<Guid, byte[]>? rowVersions = null);
        Task BulkUpdateCoStatusAsync(List<Guid> ids, int? coStatus, DateTime? coDateEndorsed, DateTime? coDateApproved, string userName, Dictionary<Guid, byte[]>? rowVersions = null);
        Task ReplaceBeneficiaryAsync(Guid outgoingHistoryId, Guid incomingHistoryId, DateTime? replacementDate, string? remarks, string userName);
        Task UndoReplacementAsync(Guid historyId, string userName);
        Task<List<BeneficiaryLookupDto>> SearchBeneficiaryLookupAsync(string? search, Guid excludeId);
        Task BulkAssignRefNumberAsync(List<Guid> ids, int quarter, string batch, int refYear, string userName);
        Task<PossibleDuplicateSummaryDto> GetPossibleDuplicatesAsync(BeneficiaryFilterDto filter);
        Task<PossibleDuplicatePairDto> ResolveDuplicatePairAsync(Guid record1Id, Guid record2Id, string? remarks, string resolvedBy);
        Task<PossibleDuplicatePairDto> UnresolveDuplicatePairAsync(Guid record1Id, Guid record2Id, string unresolvedBy);
        Task<Guid> QueuePayrollGenerationAsync(PayrollSettingsDto settings, string userName);
        Task<PossibleDuplicateSummaryDto> GetGlobalDuplicateSummaryAsync();
        Task<PagedResultDto<LogEntryDto>> GetAllLogsAsync(LogFilterDto filter);
        Task<PagedResultDto<BeneficiaryListItemDto>> SearchSimilarNamesAsync(BeneficiaryFilterDto filter);
        Task<List<PaymentHistoryDto>> GetPaymentHistoryAsync(Guid beneficiaryId);
        Task BulkAddPaymentHistoryAsync(
            List<Guid> beneficiaryIds, int? payrollQuarter, int? fiscalYear,
            int paymentStatus, int? modeOfPayment, DateTime? paymentDate,
            string? remarks, string userName);
        Task EditPaymentHistoryEntryAsync(
            Guid historyId, Guid beneficiaryId, int? payrollQuarter, int? fiscalYear,
            int paymentStatus, int? modeOfPayment, DateTime? paymentDate,
            string? remarks, string userName);
        Task DeletePaymentHistoryAsync(Guid historyId, string userName);
        Task<List<CgpRangeMemberDto>> GetCgpRangeMembersAsync(Guid cgpGenerationId, int municipalityCode, int milestoneYear);
        Task SetCurrentPaymentHistoryAsync(Guid beneficiaryId, Guid historyId, string userName);
    }
}
