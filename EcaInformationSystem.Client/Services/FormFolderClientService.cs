using EcaInformationSystem.Shared.DTOs;
using Microsoft.Extensions.Caching.Memory;
using System.Net.Http.Json;

namespace EcaInformationSystem.Client.Services
{
    public class FormFolderClientService
    {
        private readonly HttpClient _http;
        private readonly IMemoryCache _cache;
        private const string CacheKey = "formfolders_all_v1";

        private static readonly MemoryCacheEntryOptions CacheOptions = new()
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10),
            SlidingExpiration = TimeSpan.FromMinutes(5)
        };

        public FormFolderClientService(HttpClient http, IMemoryCache cache)
        {
            _http = http;
            _cache = cache;
        }

        // Called after every mutation below. The TTL above is only a safety net
        // for staleness between explicit invalidations (e.g. another tab editing
        // folders) — the normal path always invalidates right after a write.
        public void Invalidate() => _cache.Remove(CacheKey);

        public async Task<List<FormFolderDto>> GetAllAsync()
        {
            if (_cache.TryGetValue(CacheKey, out List<FormFolderDto>? cached) && cached is not null)
                return cached;

            var result = await _http.GetFromJsonAsync<List<FormFolderDto>>("api/formfolder") ?? new();
            _cache.Set(CacheKey, result, CacheOptions);
            return result;
        }

        public async Task<(bool Success, string? Error)> CreateAsync(FormFolderCreateDto dto)
        {
            var response = await _http.PostAsJsonAsync("api/formfolder", dto);
            if (response.IsSuccessStatusCode) { Invalidate(); return (true, null); }
            return (false, await response.Content.ReadAsStringAsync());
        }

        public async Task<(bool Success, string? Error)> UpdateAsync(Guid id, FormFolderUpdateDto dto)
        {
            var response = await _http.PutAsJsonAsync($"api/formfolder/{id}", dto);
            if (response.IsSuccessStatusCode) { Invalidate(); return (true, null); }
            return (false, await response.Content.ReadAsStringAsync());
        }

        // Returns affected document count on success
        public async Task<(bool Success, int AffectedCount, string? Error)> DeleteAsync(Guid id)
        {
            var response = await _http.DeleteAsync($"api/formfolder/{id}");
            if (response.IsSuccessStatusCode)
            {
                var count = await response.Content.ReadFromJsonAsync<int>();
                Invalidate();
                return (true, count, null);
            }
            return (false, 0, await response.Content.ReadAsStringAsync());
        }

        public async Task<List<FormActivityLogDto>> GetActivityLogAsync(int take = 100)
            => await _http.GetFromJsonAsync<List<FormActivityLogDto>>($"api/formfolder/activity-log?take={take}") ?? new();
    }
}
