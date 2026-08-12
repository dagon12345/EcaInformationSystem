using EcaInformationSystem.Shared.DTOs;
using System.Net.Http.Json;

namespace EcaInformationSystem.Client.Services
{
    public class DirectoryClientService
    {
        private readonly HttpClient _http;

        public DirectoryClientService(HttpClient http)
        {
            _http = http;
        }

        public async Task<List<ProvinceDirectoryGroupDto>> GetProvincialAsync()
            => await _http.GetFromJsonAsync<List<ProvinceDirectoryGroupDto>>("api/directory/provincial") ?? new();
    }
}
