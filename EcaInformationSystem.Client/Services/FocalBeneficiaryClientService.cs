using EcaInformationSystem.Shared.DTOs;
using System.Net.Http.Json;

namespace EcaInformationSystem.Client.Services
{
    public class FocalBeneficiaryClientService
    {
        private readonly HttpClient _http;

        public FocalBeneficiaryClientService(HttpClient http)
        {
            _http = http;
        }

        public async Task<FocalBeneficiaryPageDto?> GetPagedAsync(FocalBeneficiaryFilterDto filter)
        {
            var response = await _http.PostAsJsonAsync("api/focal/beneficiaries", filter);
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadFromJsonAsync<FocalBeneficiaryPageDto>();
        }

        public async Task<FocalBeneficiaryDetailDto?> GetDetailAsync(Guid id)
        {
            try { return await _http.GetFromJsonAsync<FocalBeneficiaryDetailDto>($"api/focal/beneficiaries/{id}"); }
            catch { return null; }
        }
    }
}
