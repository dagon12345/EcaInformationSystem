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

        public async Task<(bool Success, string? Error, FormDocumentDto? Document)> UploadAsync(
            Stream fileStream, string fileName, string contentType,
            string title, string? description, string? category, Guid? folderId,
            ShrinkQuality? shrinkQuality = null)
        {
            using var content = new MultipartFormDataContent();
            using var streamContent = new StreamContent(fileStream);
            streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);

            content.Add(streamContent, "file", fileName);
            content.Add(new StringContent(title), "title");
            if (!string.IsNullOrWhiteSpace(description)) content.Add(new StringContent(description), "description");
            if (!string.IsNullOrWhiteSpace(category)) content.Add(new StringContent(category), "category");
            if (folderId.HasValue) content.Add(new StringContent(folderId.Value.ToString()), "folderId");
            if (shrinkQuality.HasValue) content.Add(new StringContent(shrinkQuality.Value.ToString()), "shrinkQuality");

            HttpResponseMessage response;
            try
            {
                response = await _http.PostAsync("api/formdocument/upload", content);
            }
            catch (Exception ex)
            {
                // The server enforces a request-size limit at the framework level
                // (RequestSizeLimit), which aborts the connection mid-stream rather
                // than returning a normal HTTP response — that surfaces here as an
                // HttpRequestException/TaskCanceledException, not a status code, so
                // it has to be caught explicitly instead of falling through to the
                // IsSuccessStatusCode check below.
                return (false, $"The upload connection was interrupted: {ex.Message}. This can happen when the file is too large or the network drops mid-upload.", null);
            }

            if (response.IsSuccessStatusCode)
            {
                InvalidateAll();
                var dto = await response.Content.ReadFromJsonAsync<FormDocumentDto>();
                return (true, null, dto);
            }

            var error = await response.Content.ReadAsStringAsync();
            return (false, error, null);
        }

        // Shrinks the file server-side for the chosen quality WITHOUT saving it,
        // so the UI can show "estimated result: X MB" before the user commits.
        // The result includes a PreviewToken — pass it to UploadFromPreviewAsync
        // to confirm without re-sending or re-shrinking the file.
        public async Task<(bool Success, string? Error, ShrinkPreviewResultDto? Result)> PreviewShrinkAsync(
            Stream fileStream, string fileName, string contentType, ShrinkQuality quality)
        {
            using var content = new MultipartFormDataContent();
            using var streamContent = new StreamContent(fileStream);
            streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);

            content.Add(streamContent, "file", fileName);
            content.Add(new StringContent(quality.ToString()), "quality");

            HttpResponseMessage response;
            try
            {
                response = await _http.PostAsync("api/formdocument/shrink-preview", content);
            }
            catch (Exception ex)
            {
                return (false, $"Preview failed: {ex.Message}", null);
            }

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<ShrinkPreviewResultDto>();
                return (true, null, result);
            }

            return (false, await response.Content.ReadAsStringAsync(), null);
        }

        // Confirms an already-previewed shrink result by token — no file bytes
        // are sent again, the server already has them cached.
        public async Task<(bool Success, string? Error, FormDocumentDto? Document)> UploadFromPreviewAsync(
            Guid previewToken, string title, string? description, string? category, Guid? folderId)
        {
            using var content = new MultipartFormDataContent();
            content.Add(new StringContent(previewToken.ToString()), "previewToken");
            content.Add(new StringContent(title), "title");
            if (!string.IsNullOrWhiteSpace(description)) content.Add(new StringContent(description), "description");
            if (!string.IsNullOrWhiteSpace(category)) content.Add(new StringContent(category), "category");
            if (folderId.HasValue) content.Add(new StringContent(folderId.Value.ToString()), "folderId");

            var response = await _http.PostAsync("api/formdocument/upload", content);

            if (response.IsSuccessStatusCode)
            {
                InvalidateAll();
                var dto = await response.Content.ReadFromJsonAsync<FormDocumentDto>();
                return (true, null, dto);
            }

            return (false, await response.Content.ReadAsStringAsync(), null);
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
