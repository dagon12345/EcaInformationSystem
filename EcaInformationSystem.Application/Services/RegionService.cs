using EcaInformationSystem.Application.Interfaces;
using EcaInformationSystem.Domain.Entities;

namespace EcaInformationSystem.Application.Services
{
    public class RegionService : IRegionService
    {
        private readonly IRegionRepository _regionRepository;
        public RegionService(IRegionRepository regionRepository)
        {
            _regionRepository = regionRepository;
        }
        public Task<IEnumerable<Region>> GetAllAsync()
        {
            var regions = _regionRepository.GetAllAsync();
            return regions;
        }
    }
}
