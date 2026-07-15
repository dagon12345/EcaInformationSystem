using EcaInformationSystem.Shared.DTOs;
using System.Net.Http.Json;

namespace EcaInformationSystem.Client.Services
{
    public class FormFolderClientService
    {
        private readonly HttpClient _http;

        public FormFolderClientService(HttpClient http)
        {
            _http = http;
        }

        public async Task<List<FormFolderDto>> GetAllAsync()
            => await _http.GetFromJsonAsync<List<FormFolderDto>>("api/formfolder") ?? new();

        public async Task<(bool Success, string? Error)> CreateAsync(FormFolderCreateDto dto)
        {
            var response = await _http.PostAsJsonAsync("api/formfolder", dto);
            if (response.IsSuccessStatusCode) return (true, null);
            return (false, await response.Content.ReadAsStringAsync());
        }

        public async Task<(bool Success, string? Error)> UpdateAsync(Guid id, FormFolderUpdateDto dto)
        {
            var response = await _http.PutAsJsonAsync($"api/formfolder/{id}", dto);
            if (response.IsSuccessStatusCode) return (true, null);
            return (false, await response.Content.ReadAsStringAsync());
        }

        // Returns affected document count on success
        public async Task<(bool Success, int AffectedCount, string? Error)> DeleteAsync(Guid id)
        {
            var response = await _http.DeleteAsync($"api/formfolder/{id}");
            if (response.IsSuccessStatusCode)
            {
                var count = await response.Content.ReadFromJsonAsync<int>();
                return (true, count, null);
            }
            return (false, 0, await response.Content.ReadAsStringAsync());
        }

        public async Task<List<FormActivityLogDto>> GetActivityLogAsync(int take = 100)
            => await _http.GetFromJsonAsync<List<FormActivityLogDto>>($"api/formfolder/activity-log?take={take}") ?? new();
    }
}