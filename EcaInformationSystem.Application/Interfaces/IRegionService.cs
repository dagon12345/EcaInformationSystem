using EcaInformationSystem.Domain.Entities;

namespace EcaInformationSystem.Application.Interfaces
{
    public interface IRegionService
    {
        Task<IEnumerable<Region>> GetAllAsync();
    }
}
