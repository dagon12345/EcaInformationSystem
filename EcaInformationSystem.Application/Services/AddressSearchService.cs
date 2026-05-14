using EcaInformationSystem.Application.Interfaces.Repositories;
using EcaInformationSystem.Application.Interfaces.Services;
using EcaInformationSystem.Shared.DTOs;

namespace EcaInformationSystem.Application.Services;

public class AddressSearchService(IAddressSearchRepository repo) : IAddressSearchService
{
    public Task<IEnumerable<AddressSearchResultDto>> SearchAsync(string query, CancellationToken cancellationToken = default)
        => repo.SearchAsync(query, cancellationToken: cancellationToken);
}
