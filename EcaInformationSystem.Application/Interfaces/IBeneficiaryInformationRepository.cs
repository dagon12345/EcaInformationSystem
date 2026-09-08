using EcaInformationSystem.Domain.Entities;
using EcaInformationSystem.Shared.DTOs;

namespace EcaInformationSystem.Application.Interfaces
{
    public interface IBeneficiaryInformationRepository
    {
        Task<IEnumerable<BeneficiaryInformationDto>> GetAllAsync();
        Task<PagedResultDto<BeneficiaryListItemDto>> GetPagedListAsync(BeneficiaryFilterDto filter);
        Task<int> CountMatchingAsync(BeneficiaryFilterDto filter);
        Task<List<BatchCodeCoStatusSummaryDto>> GetEndorsedBatchCodeSummaryAsync();
        Task<PagedResultDto<BeneficiaryInformationDto>> GetPagedAsync(BeneficiaryFilterDto filter);
        Task<BeneficiaryInformationDto?> GetByIdAsync(Guid id);
        Task<BeneficiaryInformation?> GetEntityByIdAsync(Guid id);
        Task<IEnumerable<BeneficiaryInformationDto>> FilterAsync(BeneficiaryFilterDto filter);
        Task<BeneficiarySummaryResultDto> GetSummaryAsync(BeneficiaryFilterDto filter);
        Task UpdateAsync(BeneficiaryInformation beneficiaryInformation);
        Task AddAsync(BeneficiaryInformation beneficiaryInformation);
        // ✅ Clean abstraction — no EF Core reference needed by caller
        void SetOriginalRowVersion(BeneficiaryInformation entity, byte[] rowVersion);
        Task SaveChangesAsync();
        Task<bool> ExistsDuplicateAsync(string? lastName,
            string? firstName,
            string? middleName,
            DateTime birthDate,
            Guid? excludeId = null);

        // Same exact-match rule as ExistsDuplicateAsync, but returns the
        // matched record (display-ready, same shape as a soft-duplicate
        // candidate) instead of just a bool — used by Create to show the
        // Known Duplicate confirmation before the user acknowledges and
        // saves anyway (see BeneficiaryDuplicateHistory).
        Task<SoftDuplicateCandidateDto?> FindExactDuplicateAsync(string? lastName,
            string? firstName,
            string? middleName,
            DateTime birthDate);

        // Records that `entry.BeneficiaryInformationId` was saved as a Known
        // Duplicate of `entry.DuplicateOfId` — deliberately separate from the
        // beneficiary record itself, so it never surfaces in the grid/list/
        // statistics, only via GetDuplicateHistoryAsync for that one grantee.
        Task AddDuplicateHistoryAsync(BeneficiaryDuplicateHistory entry);

        // Every Known Duplicate entry involving this beneficiary, on EITHER
        // side of the pair (it was saved as a duplicate of someone, or someone
        // else was later saved as a duplicate of it) — display-ready.
        Task<List<BeneficiaryDuplicateHistoryDto>> GetDuplicateHistoryAsync(Guid beneficiaryId);

        Task<int?> GetRegionCodeByNameAsync(string regionName);
        Task<int?> GetProvinceCodeByNameAsync(string provinceName);
        Task<int?> GetMunicipalityCodeByNameAsync(string municipalityName);
        Task<int?> GetBarangayCodeByNameAsync(string barangayName);
        Task<BeneficiaryInformation?> FindExistingAsync(string? lastName, string? firstName, string? middleName, DateTime birthDate);
        Task<List<BeneficiaryInformationDto>> GetByIdsAsync(List<Guid> ids);
        Task<List<SoftDuplicateCandidateDto>> FindSoftDuplicatesAsync(string? firstName, string? lastName, DateTime birthDate, int birthdateToleranceDays = 365);
        Task BulkUpdateEligibilityAndBatchCodeAsync(List<Guid> ids, bool? isEligible, string? batchCode, Dictionary<Guid, byte[]>? rowVersions = null);
        Task BulkUpdateCoStatusAsync(List<Guid> ids, int? coStatus, DateTime? coDateEndorsed, DateTime? coDateApproved, Dictionary<Guid, byte[]>? rowVersions = null);
        Task<(BeneficiaryPaymentHistory Outgoing, BeneficiaryPaymentHistory Incoming)> ReplaceBeneficiaryAsync(Guid outgoingHistoryId, Guid incomingHistoryId, DateTime? replacementDate, string? remarks);
        Task<BeneficiaryPaymentHistory> UndoReplacementAsync(Guid historyId);
        Task<List<BeneficiaryLookupDto>> SearchBeneficiaryLookupAsync(string? search, Guid excludeId);
        Task<List<BeneficiaryInformation>> GetEntitiesByIdsAsync(List<Guid> ids);
        Task<List<PossibleDuplicatePairDto>> FindAllPossibleDuplicatesAsync(BeneficiaryFilterDto filter, int maxPairs = 50, CancellationToken cancellationToken = default);
        Task<StatisticsReportDto> GetStatisticsReportAsync(StatisticsRequestDto request);
        Task<StatisticsMembersPagedResultDto> GetStatisticsMembersAsync(StatisticsRequestDto request, string bucket, int pageNumber, int pageSize);
        Task BulkSetCgpAssignmentsAsync(List<CgpAssignmentDto> assignments);
        Task<List<Guid>> FindSimilarNameIdsAsync(string term, int maxResults = 50, double minScore = 0.75);
        Task<List<PaymentHistoryDto>> GetPaymentHistoryAsync(Guid beneficiaryId);
        Task BulkAddPaymentHistoryAsync(List<Guid> beneficiaryIds, int? payrollQuarter, int? fiscalYear,
            int paymentStatus, int? modeOfPayment, DateTime? paymentDate, string? remarks, string userName);
        Task EditPaymentHistoryEntryAsync(Guid historyId, int? payrollQuarter, int? fiscalYear,
            int paymentStatus, int? modeOfPayment, DateTime? paymentDate, string? remarks, string userName);
        Task<PaymentHistoryDeletedInfoDto> DeletePaymentHistoryAsync(Guid historyId);
        Task<List<CgpRangeMemberDto>> GetCgpRangeMembersAsync(Guid cgpGenerationId, int municipalityCode, int milestoneYear);
        Task<List<CgpRangeCandidateDto>> GetCgpRangeCandidatesAsync(List<(Guid CgpGenerationId, int MunicipalityCode)> keys);
        Task SetCurrentPaymentHistoryAsync(Guid beneficiaryId, Guid historyId, string userName);
        Task AddPaymentHistoryEntryAsync(BeneficiaryPaymentHistory entry);
        Task<List<DuplicateCheckCandidateDto>> GetDuplicateCheckPoolAsync();

        // ── Annex A sub-entities — loaded/saved alongside the main record ──────────
        Task<List<BeneficiaryFamilyMember>> GetFamilyMembersAsync(Guid beneficiaryId);
        Task ReplaceFamilyMembersAsync(Guid beneficiaryId, List<BeneficiaryFamilyMember> members);
        Task<List<BeneficiaryPhoneNumber>> GetPhoneNumbersAsync(Guid beneficiaryId);
        Task ReplacePhoneNumbersAsync(Guid beneficiaryId, List<BeneficiaryPhoneNumber> numbers);

        Task<BeneficiaryBankAccount?> GetBankAccountAsync(Guid beneficiaryId);
        Task UpsertBankAccountAsync(Guid beneficiaryId, BeneficiaryBankAccount account);

        Task<BeneficiaryAbroadAddress?> GetAbroadAddressAsync(Guid beneficiaryId);
        Task UpsertAbroadAddressAsync(Guid beneficiaryId, BeneficiaryAbroadAddress address);
        Task DeleteAbroadAddressAsync(Guid beneficiaryId);

        Task<BeneficiaryClaimant?> GetClaimantAsync(Guid beneficiaryId);
        Task UpsertClaimantAsync(Guid beneficiaryId, BeneficiaryClaimant claimant);
        Task DeleteClaimantAsync(Guid beneficiaryId);
        Task<BeneficiaryClaimantBankAccount?> GetClaimantBankAccountAsync(Guid beneficiaryId);
        Task UpsertClaimantBankAccountAsync(Guid beneficiaryId, BeneficiaryClaimantBankAccount account);
        Task DeleteClaimantBankAccountAsync(Guid beneficiaryId);
    }
}
