using EcaInformationSystem.Shared.DTOs.DailyAccomplishmentReport;
using System.Net.Http.Json;

namespace EcaInformationSystem.Client.Services
{
    public class DarReportClientService
    {
        private readonly HttpClient _http;

        public DarReportClientService(HttpClient http)
        {
            _http = http;
        }

        public async Task<List<DarReportListItemDto>> GetMineAsync()
            => await _http.GetFromJsonAsync<List<DarReportListItemDto>>("api/darreport") ?? new();

        public async Task<DarReportDto?> GetByIdAsync(Guid id)
        {
            var response = await _http.GetAsync($"api/darreport/{id}");
            return response.IsSuccessStatusCode
                ? await response.Content.ReadFromJsonAsync<DarReportDto>()
                : null;
        }

        public async Task<(bool Success, DarReportDto? Report, string? Error)> UpsertAsync(DarReportUpsertDto dto)
        {
            var response = await _http.PostAsJsonAsync("api/darreport", dto);
            if (!response.IsSuccessStatusCode)
                return (false, null, await response.Content.ReadAsStringAsync());

            var report = await response.Content.ReadFromJsonAsync<DarReportDto>();
            return (true, report, null);
        }

        public async Task<bool> DeleteAsync(Guid id)
        {
            var response = await _http.DeleteAsync($"api/darreport/{id}");
            return response.IsSuccessStatusCode;
        }
    }
}
