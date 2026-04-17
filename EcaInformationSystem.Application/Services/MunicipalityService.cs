using EcaInformationSystem.Application.Interfaces;
using EcaInformationSystem.Domain.Entities;

namespace EcaInformationSystem.Application.Services
{
    public class MunicipalityService : IMunicipalityService
    {
        private readonly IMunicipalityRepository _municipalityRepository;

        public MunicipalityService(IMunicipalityRepository municipalityRepository)
        {
            _municipalityRepository = municipalityRepository;
        }
        public async Task<IEnumerable<Municipality>> GetByProvinceCodeAsync(int psgcCodeProvince)
        {
            var municipalities = await _municipalityRepository.GetByProvinceCodeAsync(psgcCodeProvince);
            return municipalities;
        }

        public async Task<IEnumerable<Municipality>> GetByProvinceIdsAsync(IEnumerable<int> provinceIds)
        {
            var result = await _municipalityRepository.GetByProvinceIdsAsync(provinceIds);
            return result.Select(x => new Municipality
            {
                PsgcCodeProvince = x.PsgcCodeProvince,
                PsgcCodeMunicipality = x.PsgcCodeMunicipality,
                Name = x.Name ?? string.Empty
            });
        }

        public async Task<IEnumerable<Municipality>> GetMunicipalitiesAsync()
        {
            return await _municipalityRepository.GetAllMunicipalityAsync();
        }
    }
}
