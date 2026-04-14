using EcaInformationSystem.Application.Interfaces;
using EcaInformationSystem.Domain.Entities;

namespace EcaInformationSystem.Application.Services
{
    public class BarangayService : IBarangayService
    {
        private readonly IBarangayRepository _barangayRepository;

        public BarangayService(IBarangayRepository barangayRepository)
        {
            _barangayRepository = barangayRepository;
        }

        public async Task<IEnumerable<Barangay>> GetBarangaysAsync()
        {
            return await _barangayRepository.GetBarangaysAsync();
        }

        public async Task<IEnumerable<Barangay>> GetByMunicipalityCodeAsync(int psgcCodeMunicipality)
        {
            return await _barangayRepository.GetByMunicipalityCodeAsync(psgcCodeMunicipality);
        }
    }
}
