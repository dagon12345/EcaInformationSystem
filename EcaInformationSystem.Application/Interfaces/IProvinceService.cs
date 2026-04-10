using EcaInformationSystem.Domain.Entities;

namespace EcaInformationSystem.Application.Interfaces
{
    public interface IProvinceService
    {
        Task<IEnumerable<Province>> GetByRegionCodeAsync(int psgcCodeRegion);
    }
}
