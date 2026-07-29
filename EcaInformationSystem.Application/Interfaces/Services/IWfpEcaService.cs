using EcaInformationSystem.Shared.DTOs;

namespace EcaInformationSystem.Application.Interfaces.Services
{
    public interface IWfpEcaService
    {
        Task<WfpEcaDto> GetAsync(int regionCode, int fiscalYear);

        Task<WfpEcaDto> UpsertAsync(int regionCode, int fiscalYear, List<UpsertWfpEcaLineDto> lines, string userName);
    }
}
