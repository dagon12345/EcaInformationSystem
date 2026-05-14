using EcaInformationSystem.Shared.DTOs;

namespace EcaInformationSystem.Application.Interfaces.Services;

public interface IAddressSearchService
{
    Task<IEnumerable<AddressSearchResultDto>> SearchAsync(string query, CancellationToken cancellationToken = default);
}
