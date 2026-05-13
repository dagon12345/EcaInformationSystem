using EcaInformationSystem.Shared.DTOs;
using System.Net.Http.Json;

namespace EcaInformationSystem.Client.Services
{
    public class BeneficiaryFindingClientService
    {
        private readonly HttpClient _http;

        public BeneficiaryFindingClientService(HttpClient http)
        {
            _http = http;
        }

        public async Task<BeneficiaryFindingDto?> GetByBeneficiaryIdAsync(Guid beneficiaryId)
        {
            try
            {
                return await _http.GetFromJsonAsync<BeneficiaryFindingDto>(
                    $"api/beneficiary-finding/{beneficiaryId}");
            }
            catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null; // No finding yet — that's fine
            }
        }

        public async Task<(bool Success, string? Error)> UpsertAsync(
            Guid beneficiaryId,
            UpsertBeneficiaryFindingDto dto)
        {
            var response = await _http.PostAsJsonAsync(
                $"api/beneficiary-finding/{beneficiaryId}", dto);

            if (response.IsSuccessStatusCode)
                return (true, null);

            var error = await response.Content.ReadAsStringAsync();
            return (false, error);
        }
    }
}
