using EcaInformationSystem.Shared.DTOs;
using Microsoft.Extensions.Caching.Memory;
using System.Net.Http.Json;

namespace EcaInformationSystem.Client.Services
{
    public class FormDocumentClientService
    {
        private readonly HttpClient _http;
        private readonly IMemoryCache _cache;
        private readonly FormFolderClientService _folderService;
        private const string CacheKey = "formdocuments_all_v1";

        private static readonly MemoryCacheEntryOptions CacheOptions = new()
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10),
            SlidingExpiration = TimeSpan.FromMinutes(5)
        };

        public FormDocumentClientService(HttpClient http, IMemoryCache cache, FormFolderClientService folderService)
        {
            _http = http;
            _cache = cache;
            _folderService = folderService;
        }

        // Called after every mutation below. The TTL above is only a safety net
        // for staleness between explicit invalidations — the normal path always
        // invalidates right after a write.
        public void Invalidate() => _cache.Remove(CacheKey);

        // Every document upload/edit/delete can change a folder's DocumentCount
        // (moving a file in/out, or deleting it) — the folder list cache holds
        // those counts, so it has to be invalidated too, not just this one.
        private void InvalidateAll()
        {
            Invalidate();
            _folderService.Invalidate();
        }

        private async Task<List<FormDocumentDto>> GetAllCachedAsync()
        {
            if (_cache.TryGetValue(CacheKey, out List<FormDocumentDto>? cached) && cached is not null)
                return cached;

            var result = await _http.GetFromJsonAsync<List<FormDocumentDto>>("api/formdocument") ?? new();
            _cache.Set(CacheKey, result, CacheOptions);
            return result;
        }

        public async Task<List<FormDocumentDto>> GetAllAsync() => await GetAllCachedAsync();

        // Filters/sorts the cached full list client-side — every folder switch
        // and search keystroke used to be its own API round trip; now it's only
        // one request per cache window (or right after a mutation invalidates it).
        public async Task<List<FormDocumentDto>> SearchAsync(FormDocumentSearchDto filter)
        {
            var docs = await GetAllCachedAsync();
            IEnumerable<FormDocumentDto> query = docs;

            if (filter.UncategorizedOnly)
            {
                query = query.Where(d => d.FolderId == null);
            }
            else if (filter.FolderId.HasValue)
            {
                query = query.Where(d => d.FolderId == filter.FolderId.Value);
            }

            if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
            {
                var term = filter.SearchTerm.Trim().ToLowerInvariant();
                query = query.Where(d =>
                    d.Title.ToLowerInvariant().Contains(term) ||
                    (d.Description != null && d.Description.ToLowerInvariant().Contains(term)) ||
                    (d.Category != null && d.Category.ToLowerInvariant().Contains(term)) ||
                    d.OriginalFileName.ToLowerInvariant().Contains(term));
            }

            query = filter.SortBy == "Name"
                ? (filter.SortAscending
                    ? query.OrderBy(d => d.Title, StringComparer.OrdinalIgnoreCase)
                    : query.OrderByDescending(d => d.Title, StringComparer.OrdinalIgnoreCase))
                : (filter.SortAscending
                    ? query.OrderBy(d => d.UploadedAt)
                    : query.OrderByDescending(d => d.UploadedAt));

            return query.ToList();
        }

        public async Task<(bool Success, string? Error)> UploadAsync(
            Stream fileStream, string fileName, string contentType,
            string title, string? description, string? category, Guid? folderId)
        {
            using var content = new MultipartFormDataContent();
            using var streamContent = new StreamContent(fileStream);
            streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);

            content.Add(streamContent, "file", fileName);
            content.Add(new StringContent(title), "title");
            if (!string.IsNullOrWhiteSpace(description)) content.Add(new StringContent(description), "description");
            if (!string.IsNullOrWhiteSpace(category)) content.Add(new StringContent(category), "category");
            if (folderId.HasValue) content.Add(new StringContent(folderId.Value.ToString()), "folderId");

            var response = await _http.PostAsync("api/formdocument/upload", content);
            if (response.IsSuccessStatusCode) { InvalidateAll(); return (true, null); }

            var error = await response.Content.ReadAsStringAsync();
            return (false, error);
        }

        public async Task<(bool Success, string? Error)> UpdateMetadataAsync(Guid id, FormDocumentUpdateDto dto)
        {
            var response = await _http.PutAsJsonAsync($"api/formdocument/{id}", dto);
            if (response.IsSuccessStatusCode) { InvalidateAll(); return (true, null); }
            return (false, await response.Content.ReadAsStringAsync());
        }

        public async Task<(bool Success, string? Error)> DeleteAsync(Guid id)
        {
            var response = await _http.DeleteAsync($"api/formdocument/{id}");
            if (response.IsSuccessStatusCode) { InvalidateAll(); return (true, null); }
            return (false, await response.Content.ReadAsStringAsync());
        }

        public async Task<(byte[] Data, string FileName, string ContentType)?> DownloadAsync(Guid id, string fallbackFileName)
        {
            var response = await _http.GetAsync($"api/formdocument/{id}/download");
            if (!response.IsSuccessStatusCode) return null;

            var bytes = await response.Content.ReadAsByteArrayAsync();
            var contentType = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";
            var fileName = response.Content.Headers.ContentDisposition?.FileNameStar
                ?? response.Content.Headers.ContentDisposition?.FileName
                ?? fallbackFileName;

            return (bytes, fileName.Trim('"'), contentType);
        }
    }
}
