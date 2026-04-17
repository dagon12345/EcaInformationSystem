using EcaInformationSystem.Domain.Entities;

namespace EcaInformationSystem.Application.Interfaces
{
    public interface IMunicipalityService
    {
        Task<IEnumerable<Municipality>> GetByProvinceCodeAsync(int psgcCodeProvince);
        Task<IEnumerable<Municipality>> GetMunicipalitiesAsync();
        Task<IEnumerable<Municipality>> GetByProvinceIdsAsync(IEnumerable<int> provinceIds);
    }
}
