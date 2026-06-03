using EcaInformationService.Shared.DTOs;
using EcaInformationSystem.Domain.Entities;
using EcaInformationSystem.Shared.DTOs;

namespace EcaInformationSystem.Application.Interfaces
{
    public interface IBeneficiaryInformationRepository
    {
        Task<IEnumerable<BeneficiaryInformationDto>> GetAllAsync();
        Task<PagedResultDto<BeneficiaryInformationDto>> GetPagedAsync(BeneficiaryFilterDto filter);
        Task<BeneficiaryInformationDto?> GetByIdAsync(Guid id);
        Task<BeneficiaryInformation?> GetEntityByIdAsync(Guid id);
        Task<IEnumerable<BeneficiaryInformationDto>> FilterAsync(BeneficiaryFilterDto filter);
        Task<BeneficiarySummaryResultDto> GetSummaryAsync(BeneficiaryFilterDto filter);
        Task AddAsync(BeneficiaryInformation beneficiaryInformation);
        Task UpdateAsync(BeneficiaryInformation beneficiaryInformation);
        Task SaveChangesAsync();
        Task<bool> ExistsDuplicateAsync(string? lastName,
            string? firstName,
            string? middleName,
            DateTime birthDate,
            Guid? excludeId = null);

        Task<int?> GetRegionCodeByNameAsync(string regionName);
        Task<int?> GetProvinceCodeByNameAsync(string provinceName);
        Task<int?> GetMunicipalityCodeByNameAsync(string municipalityName);
        Task<int?> GetBarangayCodeByNameAsync(string barangayName);
        Task<BeneficiaryInformation?> FindExistingAsync(string? lastName,string? firstName, string? middleName, DateTime birthDate);

        Task BulkUpdatePaymentStatusAsync(List<Guid> ids, int paymentStatus, DateTime? paymentDate);
        Task<List<BeneficiaryInformationDto>> GetByIdsAsync(List<Guid> ids);
        Task<List<SoftDuplicateCandidateDto>> FindSoftDuplicatesAsync(string? firstName, string? lastName, DateTime birthDate, int birthdateToleranceDays = 365);
        Task BulkUpdateEligibilityAndBatchCodeAsync(List<Guid> ids, bool? isEligible, string? batchCode);
        Task BulkUpdateCoStatusAsync(List<Guid> ids, int? coStatus, DateTime? coDateEndorsed, DateTime? coDateApproved);
        Task<List<BeneficiaryInformation>> GetEntitiesByIdsAsync(List<Guid> ids);
    }
}
