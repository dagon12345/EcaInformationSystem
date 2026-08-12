using EcaInformationSystem.Shared.DTOs;
using System.Net.Http.Json;

namespace EcaInformationSystem.Client.Services
{
    public class VoiceCallLogClientService
    {
        private readonly HttpClient _http;

        public VoiceCallLogClientService(HttpClient http)
        {
            _http = http;
        }

        // SuperAdmin/Admin get every call; anyone else gets only calls they
        // were a participant in — enforced server-side, not just here.
        public async Task<List<VoiceCallLogDto>> GetLogsAsync()
            => await _http.GetFromJsonAsync<List<VoiceCallLogDto>>("api/voicecalllog") ?? new();

        public async Task<(bool Success, VoiceCallLogDto? Log, string? Error)> UpdateNotesAsync(Guid id, string? notes)
        {
            var response = await _http.PutAsJsonAsync($"api/voicecalllog/{id}/notes", new UpdateVoiceCallLogNotesRequest { Notes = notes });
            if (!response.IsSuccessStatusCode)
                return (false, null, await response.Content.ReadAsStringAsync());

            return (true, await response.Content.ReadFromJsonAsync<VoiceCallLogDto>(), null);
        }

        public async Task<bool> DeleteAsync(Guid id)
        {
            var response = await _http.DeleteAsync($"api/voicecalllog/{id}");
            return response.IsSuccessStatusCode;
        }
    }
}
