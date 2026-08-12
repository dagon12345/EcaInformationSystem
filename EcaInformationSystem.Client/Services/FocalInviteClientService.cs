using EcaInformationSystem.Shared.DTOs;
using EcaInformationSystem.Shared.DTOs.Auth;
using System.Net.Http.Json;

namespace EcaInformationSystem.Client.Services
{
    public class FocalInviteClientService
    {
        private readonly HttpClient _http;

        public FocalInviteClientService(HttpClient http)
        {
            _http = http;
        }

        public async Task<List<MyJurisdictionDto>> GetMyJurisdictionsAsync()
            => await _http.GetFromJsonAsync<List<MyJurisdictionDto>>("api/auth/my-jurisdictions") ?? new();

        // Nationwide lookups — for Admin/SuperAdmin, who aren't scoped to any
        // particular jurisdiction and can invite a focal for any province.
        // Same endpoints UserManagement.razor's jurisdiction-assignment screen uses.
        public async Task<List<ProvinceLookupDto>> GetAllProvincesAsync()
            => await _http.GetFromJsonAsync<List<ProvinceLookupDto>>("api/Province") ?? new();

        public async Task<List<MunicipalityLookupDto>> GetMunicipalitiesByProvinceAsync(int psgcCodeProvince)
            => await _http.GetFromJsonAsync<List<MunicipalityLookupDto>>($"api/Municipality/by-province/{psgcCodeProvince}") ?? new();

        public async Task<(bool Success, FocalInviteResultDto? Result, string? Error)> CreateInviteAsync(CreateFocalInviteRequest request)
        {
            var response = await _http.PostAsJsonAsync("api/auth/invite-focal", request);
            if (!response.IsSuccessStatusCode)
                return (false, null, await response.Content.ReadAsStringAsync());

            return (true, await response.Content.ReadFromJsonAsync<FocalInviteResultDto>(), null);
        }

        public async Task<FocalInvitePreviewDto?> PreviewInviteAsync(string code)
        {
            try { return await _http.GetFromJsonAsync<FocalInvitePreviewDto>($"api/auth/focal-invite/{Uri.EscapeDataString(code)}"); }
            catch { return null; }
        }

        public async Task<(bool Success, string? Token, string? FullName, string? UserName, string? Error)> AcceptInviteAsync(AcceptFocalInviteRequest request)
        {
            var response = await _http.PostAsJsonAsync("api/auth/accept-focal-invite", request);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                return (false, null, null, null, body);
            }

            var result = await response.Content.ReadFromJsonAsync<AcceptFocalInviteResponse>();
            return (true, result?.Token, result?.FullName, result?.UserName, null);
        }

        private class AcceptFocalInviteResponse
        {
            public string Token { get; set; } = string.Empty;
            public string FullName { get; set; } = string.Empty;
            public string UserName { get; set; } = string.Empty;
        }
    }
}
