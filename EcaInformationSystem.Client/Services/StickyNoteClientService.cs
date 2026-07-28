using EcaInformationSystem.Shared.DTOs;
using Microsoft.Extensions.Caching.Memory;
using System.Net.Http.Json;

namespace EcaInformationSystem.Client.Services
{
    public class StickyNoteClientService
    {
        private readonly HttpClient _http;
        private readonly IMemoryCache _cache;
        private const string CacheKey = "stickynotes_mine_v1";

        private static readonly MemoryCacheEntryOptions CacheOptions = new()
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10),
            SlidingExpiration = TimeSpan.FromMinutes(5)
        };

        public StickyNoteClientService(HttpClient http, IMemoryCache cache)
        {
            _http = http;
            _cache = cache;
        }

        public void Invalidate() => _cache.Remove(CacheKey);

        public async Task<List<StickyNoteDto>> GetMineAsync()
        {
            if (_cache.TryGetValue(CacheKey, out List<StickyNoteDto>? cached) && cached is not null)
                return cached;

            var result = await _http.GetFromJsonAsync<List<StickyNoteDto>>("api/stickynotes") ?? new();
            _cache.Set(CacheKey, result, CacheOptions);
            return result;
        }

        public async Task<(bool Success, StickyNoteDto? Note, string? Error)> UpsertAsync(StickyNoteUpsertDto dto)
        {
            var response = await _http.PostAsJsonAsync("api/stickynotes", dto);
            if (!response.IsSuccessStatusCode)
                return (false, null, await response.Content.ReadAsStringAsync());

            var note = await response.Content.ReadFromJsonAsync<StickyNoteDto>();
            Invalidate();
            return (true, note, null);
        }

        public async Task<bool> DeleteAsync(Guid id)
        {
            var response = await _http.DeleteAsync($"api/stickynotes/{id}");
            if (response.IsSuccessStatusCode) Invalidate();
            return response.IsSuccessStatusCode;
        }
    }
}
