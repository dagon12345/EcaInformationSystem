using EcaInformationSystem.Domain.Entities;

namespace EcaInformationSystem.Application.Interfaces
{
    public interface IMunicipalityRepository
    {
        Task<IEnumerable<Municipality>> GetByProvinceCodeAsync(int psgcCodeProvince);
        Task<IEnumerable<Municipality>> GetAllMunicipalityAsync();
        Task<IEnumerable<Municipality>> GetByProvinceIdsAsync(IEnumerable<int> provinceIds);
    }
}
