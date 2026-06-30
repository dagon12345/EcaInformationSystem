using EcaInformationSystem.Shared.DTOs;
using Microsoft.JSInterop;
using System.Net.Http.Json;

namespace EcaInformationSystem.Client.Services
{
    public class DocumentUploadQueueService
    {
        private readonly HttpClient _http;
        private readonly IJSRuntime _js;
        private readonly IConfiguration _configuration;

        private readonly List<DocumentUploadJobDto> _jobs = new();
        private readonly SemaphoreSlim _lock = new(1, 1);
        private bool _isProcessing;

        // ── Fired whenever job list state changes (for the bell indicator) ────
        public event Action? OnChanged;

        // ── Fired when a job succeeds — component subscribes to refresh its list
        // Carries (beneficiaryId, uploadedFileName) so the component can match
        // on beneficiaryId and show the correct filename in the bell history.
        public event Action<Guid, string>? OnJobCompleted;

        public IReadOnlyList<DocumentUploadJobDto> Jobs => _jobs;

        public int ActiveCount => _jobs.Count(j =>
            j.Status is UploadJobStatus.Queued
                     or UploadJobStatus.Uploading
                     or UploadJobStatus.Retrying);

        public bool IsJobActive(Guid jobId)
        {
            var job = _jobs.FirstOrDefault(j => j.JobId == jobId);
            return job is not null &&
                   job.Status is UploadJobStatus.Queued
                               or UploadJobStatus.Uploading
                               or UploadJobStatus.Retrying;
        }

        private string ApiBase =>
            (_http.BaseAddress?.ToString() ?? _configuration["ApiBaseUrl"] ?? "https://REDACTED_INTERNAL_IP:8080")
            .TrimEnd('/');

        public DocumentUploadQueueService(
            HttpClient http,
            IJSRuntime js,
            IConfiguration configuration)
        {
            _http = http;
            _js = js;
            _configuration = configuration;
        }

        // ── Enqueue a camera upload — returns the job ID so the component
        // can track its specific job's active state ────────────────────────────
        public async Task<Guid> EnqueueCameraUploadAsync(
            Guid beneficiaryId,
            List<UploadFilePayload> photos)
        {
            // Build a meaningful display name: single photo uses its filename,
            // multiple photos show count. The bell will update this to the actual
            // server-assigned PDF name once the upload succeeds.
            var displayName = photos.Count == 1
                ? Path.GetFileNameWithoutExtension(photos[0].FileName) + " (photo→PDF)"
                : $"{photos.Count} photos → PDF";

            var job = new DocumentUploadJobDto
            {
                BeneficiaryId = beneficiaryId,
                FileName = displayName,
                IsCamera = true,
                Files = photos
            };

            await _lock.WaitAsync();
            try { _jobs.Insert(0, job); }
            finally { _lock.Release(); }

            NotifyChanged();
            EnsureProcessing();
            return job.JobId;
        }

        // ── Enqueue a PDF upload — returns job ID ─────────────────────────────
        public async Task<Guid> EnqueuePdfUploadAsync(
            Guid beneficiaryId,
            List<UploadFilePayload> files)
        {
            var displayName = files.Count == 1
                ? files[0].FileName
                : $"{files.Count} PDF file(s)";

            var job = new DocumentUploadJobDto
            {
                BeneficiaryId = beneficiaryId,
                FileName = displayName,
                IsCamera = false,
                Files = files
            };

            await _lock.WaitAsync();
            try { _jobs.Insert(0, job); }
            finally { _lock.Release(); }

            NotifyChanged();
            EnsureProcessing();
            return job.JobId;
        }

        public async Task DismissCompletedAsync(Guid jobId)
        {
            await _lock.WaitAsync();
            try
            {
                var job = _jobs.FirstOrDefault(j => j.JobId == jobId);
                if (job is not null &&
                    job.Status is UploadJobStatus.Done or UploadJobStatus.Failed)
                    _jobs.Remove(job);
            }
            finally { _lock.Release(); }

            NotifyChanged();
        }

        // ── Background processing ─────────────────────────────────────────────
        private void EnsureProcessing()
        {
            if (_isProcessing) return;
            _ = ProcessQueueAsync();
        }

        private async Task ProcessQueueAsync()
        {
            _isProcessing = true;
            try
            {
                while (true)
                {
                    DocumentUploadJobDto? job = null;

                    await _lock.WaitAsync();
                    try
                    {
                        job = _jobs.FirstOrDefault(j =>
                            j.Status is UploadJobStatus.Queued
                                     or UploadJobStatus.Retrying);
                    }
                    finally { _lock.Release(); }

                    if (job is null) break;

                    await ProcessJobAsync(job);
                }
            }
            finally
            {
                _isProcessing = false;
            }
        }

        private async Task ProcessJobAsync(DocumentUploadJobDto job)
        {
            job.Status = UploadJobStatus.Uploading;
            job.AttemptCount++;
            NotifyChanged();

            try
            {
                string? token = null;
                try
                {
                    token = await _js.InvokeAsync<string>(
                        "localStorage.getItem", "authToken");
                }
                catch { /* JS interop unavailable mid-navigation, proceed without */ }

                using var content = BuildContent(job);
                using var request = new HttpRequestMessage(
                    HttpMethod.Post,
                    job.IsCamera
                        ? $"{ApiBase}/api/beneficiary-documents/{job.BeneficiaryId}/upload-from-camera"
                        : $"{ApiBase}/api/beneficiary-documents/{job.BeneficiaryId}/upload")
                {
                    Content = content
                };

                if (!string.IsNullOrWhiteSpace(token))
                    request.Headers.Authorization =
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                var response = await _http.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    // ── Try to read the returned filename from the response ──
                    // Your UploadAsync returns List<BeneficiaryDocumentDto>;
                    // grab the first one's OriginalFileName for the bell display.
                    string uploadedFileName = job.FileName; // fallback
                    try
                    {
                        var docs = await response.Content
                            .ReadFromJsonAsync<List<BeneficiaryDocumentDto>>();
                        var firstName = docs?.FirstOrDefault()?.OriginalFileName;
                        if (!string.IsNullOrWhiteSpace(firstName))
                            uploadedFileName = firstName;
                        else if (docs?.Count > 1)
                            uploadedFileName = $"{docs.Count} files uploaded";
                    }
                    catch { /* response parsing failed — keep fallback name */ }

                    job.Status = UploadJobStatus.Done;
                    job.FileName = uploadedFileName; // ✅ update bell to show real filename
                    job.ErrorMessage = null;
                    NotifyChanged();

                    // ── Notify the component (if still alive) to refresh its list
                    OnJobCompleted?.Invoke(job.BeneficiaryId, uploadedFileName);
                    return;
                }

                // ── 4xx = permanent failure, do not retry ──────────────────────
                if ((int)response.StatusCode >= 400 && (int)response.StatusCode < 500)
                {
                    var body = await response.Content.ReadAsStringAsync();
                    job.Status = UploadJobStatus.Failed;
                    job.ErrorMessage = $"Rejected ({(int)response.StatusCode}): {body}";
                    NotifyChanged();
                    return;
                }

                // ── 5xx = server error, schedule retry ─────────────────────────
                job.ErrorMessage = $"Server error ({(int)response.StatusCode}), will retry...";
            }
            catch (Exception ex)
            {
                // ── Network failure (no signal, timeout) → retry ───────────────
                job.ErrorMessage = $"No connection, will retry... ({ex.Message})";
            }

            // ── Exponential backoff: 3s → 6s → 12s → 24s → 60s (capped) ──────
            job.Status = UploadJobStatus.Retrying;
            NotifyChanged();

            var delaySeconds = Math.Min(60, 3 * (int)Math.Pow(2, Math.Min(job.AttemptCount - 1, 4)));
            await Task.Delay(TimeSpan.FromSeconds(delaySeconds));

            job.Status = UploadJobStatus.Queued;
            NotifyChanged();
        }

        private static MultipartFormDataContent BuildContent(DocumentUploadJobDto job)
        {
            var content = new MultipartFormDataContent();
            foreach (var file in job.Files)
            {
                var byteContent = new ByteArrayContent(file.Bytes);
                byteContent.Headers.ContentType =
                    new System.Net.Http.Headers.MediaTypeHeaderValue(file.ContentType);
                content.Add(byteContent,
                    job.IsCamera ? "photos" : "files",
                    file.FileName);
            }
            return content;
        }

        private void NotifyChanged() => OnChanged?.Invoke();
    }
}