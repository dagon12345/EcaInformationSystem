using EcaInformationSystem.Shared.DTOs;
using Microsoft.Extensions.Caching.Memory;
using System.Net.Http.Json;

namespace EcaInformationSystem.Client.Services
{
    public class FormDocumentClientService
    {
        private readonly HttpClient _http;

        // Used only for calls that can legitimately run long — large-file
        // upload and server-side shrink-compression — which need far more than
        // the default 100s HttpClient.Timeout that every other quick call here
        // uses. See Client/Program.cs's "AuthorizedClientLongRunning" registration.
        private readonly HttpClient _longRunningHttp;
        private readonly IMemoryCache _cache;
        private readonly FormFolderClientService _folderService;
        private const string CacheKey = "formdocuments_all_v1";

        private static readonly MemoryCacheEntryOptions CacheOptions = new()
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10),
            SlidingExpiration = TimeSpan.FromMinutes(5)
        };

        public FormDocumentClientService(HttpClient http, HttpClient longRunningHttp, IMemoryCache cache, FormFolderClientService folderService)
        {
            _http = http;
            _longRunningHttp = longRunningHttp;
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
            ShrinkQuality? shrinkQuality = null,
            int? payrollQuarter = null, int? fiscalYear = null, int? psgcCodeRegion = null,
            int? psgcCodeProvince = null, int? psgcCodeMunicipality = null)
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
            if (payrollQuarter.HasValue) content.Add(new StringContent(payrollQuarter.Value.ToString()), "payrollQuarter");
            if (fiscalYear.HasValue) content.Add(new StringContent(fiscalYear.Value.ToString()), "fiscalYear");
            if (psgcCodeRegion.HasValue) content.Add(new StringContent(psgcCodeRegion.Value.ToString()), "psgcCodeRegion");
            if (psgcCodeProvince.HasValue) content.Add(new StringContent(psgcCodeProvince.Value.ToString()), "psgcCodeProvince");
            if (psgcCodeMunicipality.HasValue) content.Add(new StringContent(psgcCodeMunicipality.Value.ToString()), "psgcCodeMunicipality");

            HttpResponseMessage response;
            try
            {
                response = await _longRunningHttp.PostAsync("api/formdocument/upload", content);
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
                response = await _longRunningHttp.PostAsync("api/formdocument/shrink-preview", content);
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
            Guid previewToken, string title, string? description, string? category, Guid? folderId,
            int? payrollQuarter = null, int? fiscalYear = null, int? psgcCodeRegion = null,
            int? psgcCodeProvince = null, int? psgcCodeMunicipality = null)
        {
            using var content = new MultipartFormDataContent();
            content.Add(new StringContent(previewToken.ToString()), "previewToken");
            content.Add(new StringContent(title), "title");
            if (!string.IsNullOrWhiteSpace(description)) content.Add(new StringContent(description), "description");
            if (!string.IsNullOrWhiteSpace(category)) content.Add(new StringContent(category), "category");
            if (folderId.HasValue) content.Add(new StringContent(folderId.Value.ToString()), "folderId");
            if (payrollQuarter.HasValue) content.Add(new StringContent(payrollQuarter.Value.ToString()), "payrollQuarter");
            if (fiscalYear.HasValue) content.Add(new StringContent(fiscalYear.Value.ToString()), "fiscalYear");
            if (psgcCodeRegion.HasValue) content.Add(new StringContent(psgcCodeRegion.Value.ToString()), "psgcCodeRegion");
            if (psgcCodeProvince.HasValue) content.Add(new StringContent(psgcCodeProvince.Value.ToString()), "psgcCodeProvince");
            if (psgcCodeMunicipality.HasValue) content.Add(new StringContent(psgcCodeMunicipality.Value.ToString()), "psgcCodeMunicipality");

            var response = await _longRunningHttp.PostAsync("api/formdocument/upload", content);

            if (response.IsSuccessStatusCode)
            {
                InvalidateAll();
                var dto = await response.Content.ReadFromJsonAsync<FormDocumentDto>();
                return (true, null, dto);
            }

            return (false, await response.Content.ReadAsStringAsync(), null);
        }

        // Finds the payroll PDF an admin has tagged (via the Forms Gateway Edit
        // modal) for a given quarter/fiscal year, so a grantee's payment
        // history row can link straight to it. Payroll is often run per
        // municipality even within one region, so the preference order is:
        // 1) a document tagged for this exact municipality (most specific),
        // 2) a document tagged for the region but no specific municipality,
        // 3) a document left with no region tag at all — "applies everywhere".
        // Filters the same cached list SearchAsync uses — no extra API round
        // trip — and picks the most recently updated match within a tier if
        // more than one document happens to carry the same tags.
        public async Task<FormDocumentDto?> FindPayrollDocumentAsync(
            int payrollQuarter, int fiscalYear, int? psgcCodeRegion, int? psgcCodeMunicipality = null)
        {
            var docs = await GetAllCachedAsync();
            var candidates = docs
                .Where(d => d.PayrollQuarter == payrollQuarter && d.FiscalYear == fiscalYear)
                .ToList();

            if (candidates.Count == 0) return null;

            FormDocumentDto? BestOf(IEnumerable<FormDocumentDto> matches) =>
                matches.OrderByDescending(d => d.UpdatedAt ?? d.UploadedAt).FirstOrDefault();

            var municipalityMatch = psgcCodeMunicipality.HasValue
                ? BestOf(candidates.Where(d => d.PsgcCodeMunicipality == psgcCodeMunicipality.Value))
                : null;
            if (municipalityMatch != null) return municipalityMatch;

            var regionMatch = psgcCodeRegion.HasValue
                ? BestOf(candidates.Where(d => d.PsgcCodeRegion == psgcCodeRegion.Value && d.PsgcCodeMunicipality == null))
                : null;
            if (regionMatch != null) return regionMatch;

            return BestOf(candidates.Where(d => d.PsgcCodeRegion == null && d.PsgcCodeMunicipality == null));
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
