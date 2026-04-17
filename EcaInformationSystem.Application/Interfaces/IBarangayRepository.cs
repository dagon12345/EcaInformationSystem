using EcaInformationSystem.Domain.Entities;

namespace EcaInformationSystem.Application.Interfaces
{
    public interface IBarangayRepository
    {
        Task<IEnumerable<Barangay>> GetByMunicipalityCodeAsync(int psgcCodeMunicipality);
        Task<IEnumerable<Barangay>> GetBarangaysAsync();
        Task<IEnumerable<Barangay>> GetByMunicipalityIdsAsync(IEnumerable<int> municipalityIds);
    }
}
