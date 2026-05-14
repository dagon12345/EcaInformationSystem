using EcaInformationSystem.Shared.DTOs;

namespace EcaInformationSystem.Application.Interfaces.Repositories;

public interface IAddressSearchRepository
{
    Task<IEnumerable<AddressSearchResultDto>> SearchAsync(string query, int maxResults = 15, CancellationToken cancellationToken = default);
}
