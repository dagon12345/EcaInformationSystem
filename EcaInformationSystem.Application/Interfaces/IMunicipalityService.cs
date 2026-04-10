using EcaInformationSystem.Domain.Entities;

namespace EcaInformationSystem.Application.Interfaces
{
    public interface IMunicipalityService
    {
        Task<IEnumerable<Municipality>> GetByProvinceCodeAsync(int psgcCodeProvince);
    }
}
