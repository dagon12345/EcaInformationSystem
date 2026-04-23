using EcaInformationSystem.Application.DTOs;
using EcaInformationSystem.Domain.Entities;

namespace EcaInformationSystem.Application.Interfaces
{
    public interface IBeneficiaryInformationRepository
    {
        Task<IEnumerable<BeneficiaryInformationDto>> GetAllAsync();
        Task<PagedResultDto<BeneficiaryInformationDto>> GetPagedAsync(BeneficiaryFilterDto filter);
        Task<BeneficiaryInformation?> GetByIdAsync(Guid id);
        Task<IEnumerable<BeneficiaryInformationDto>> FilterAsync(BeneficiaryFilterDto filter);
        Task<BeneficiarySummaryResultDto> GetSummaryAsync(BeneficiaryFilterDto filter);
        Task AddAsync(BeneficiaryInformation beneficiaryInformation);
        Task UpdateAsync(BeneficiaryInformation beneficiaryInformation);
        Task SaveChangesAsync();
        Task<bool> ExistsDuplicateAsync(string? lastName,
            string? firstName,
            string? middleName,
            DateTime birthDate,
            string? oscaIdNumber,
            int? ncscRrn,
            Guid? excludeId = null);

        Task<int?> GetRegionCodeByNameAsync(string regionName);
        Task<int?> GetProvinceCodeByNameAsync(string provinceName);
        Task<int?> GetMunicipalityCodeByNameAsync(string municipalityName);
        Task<int?> GetBarangayCodeByNameAsync(string barangayName);
        Task<BeneficiaryInformation?> FindExistingAsync(
    string? lastName,
    string? firstName,
    string? middleName,
    DateTime birthDate,
    string? oscaIdNumber,
    int? ncscRrn);
    }
}
