using EcaInformationSystem.Domain.Entities;

namespace EcaInformationSystem.Application.Interfaces
{
    public interface IProvinceRepository
    {
        Task<IEnumerable<Province>> GetAllAsync(int psgcCodeRegion);
        Task<IEnumerable<Province>> GetAllProvinceAsync();
    }
}
