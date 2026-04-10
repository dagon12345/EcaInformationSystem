using EcaInformationSystem.Domain.Entities;

namespace EcaInformationSystem.Application.Interfaces
{
    public interface IBarangayService
    {
        Task<IEnumerable<Barangay>> GetByMunicipalityCodeAsync(int psgcCodeMunicipality);
    }
}
