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

        public async Task<IEnumerable<Municipality>> GetMunicipalitiesAsync()
        {
            return await _municipalityRepository.GetAllMunicipalityAsync();
        }
    }
}
