using EcaInformationSystem.Application.Interfaces;
using EcaInformationSystem.Domain.Entities;

namespace EcaInformationSystem.Application.Services
{
    public class ProvinceService : IProvinceService
    {
        private readonly IProvinceRepository _provinceRepository;
        public ProvinceService(IProvinceRepository provinceRepository)
        {
            _provinceRepository = provinceRepository;
        }
        public async Task<IEnumerable<Province>> GetByRegionCodeAsync(int psgcCodeRegion)
        {
            return await _provinceRepository.GetAllAsync(psgcCodeRegion);
        }
    }
}
