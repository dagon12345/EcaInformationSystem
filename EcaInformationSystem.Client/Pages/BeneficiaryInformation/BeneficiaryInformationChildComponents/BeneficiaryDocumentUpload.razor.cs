using System.Net.Http.Json;
using EcaInformationSystem.Shared.DTOs;
using EcaInformationSystem.Client.Services;
using Havit.Blazor.Components.Web;
using Havit.Blazor.Components.Web.Bootstrap;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;

namespace EcaInformationSystem.Client.Pages.BeneficiaryInformation
    .BeneficiaryInformationChildComponents;

public partial class BeneficiaryDocumentUpload : IDisposable
{
    [Parameter] public Guid EditId { get; set; }

    [Inject] private HttpClient Http { get; set; } = default!;
    [Inject] private IHxMessengerService Messenger { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private IConfiguration Configuration { get; set; } = default!;
    [Inject] private DocumentUploadQueueService UploadQueue { get; set; } = default!;

    [Parameter] public EventCallback OnDeleteComplete { get; set; }
    [Parameter] public EventCallback OnRequestHideOffcanvas { get; set; }
    [Parameter] public int PsgcCodeMunicipality { get; set; }

    [Inject] private AuthenticationStateProvider AuthStateProvider { get; set; } = default!;

    private HxInputFileDropZone _dropZone = default!;
    private bool _isDeleting;
    private BeneficiaryDocumentDto? _docToRename;
    private string _renameValue = string.Empty;
    private string? _renameError;
    private bool _isRenaming;
    private List<BeneficiaryDocumentDto> _documents = new();
    private BeneficiaryDocumentDto? _selectedDoc;
    private BeneficiaryDocumentDto? _docToDelete;
    private List<IBrowserFile> _pendingFiles = new();
    private bool _isLoading;
    private string? _blobUrl;
    private string? _fileValidationError;
    private int _zoomPercent = 100;
    private string ViewerSrc => $"{_blobUrl}#zoom={_zoomPercent}";
    // ── Per-component upload tracking (fed from the queue service) ────────────
    // True while ANY job for THIS beneficiary is queued/uploading/retrying.
    private bool _isCameraJobActive;
    private bool _isPdfJobActive;
    // Tracks the most recent queued job IDs for this component instance
    // so we can tell the queue to notify us specifically when they finish.
    private Guid? _lastCameraJobId;
    private Guid? _lastPdfJobId;

    private string ApiBase =>
        (Http.BaseAddress?.ToString() ?? Configuration["ApiBaseUrl"] ?? "https://REDACTED_INTERNAL_IP:8080")
        .TrimEnd('/');

    private string UploadButtonText => _isPdfJobActive
        ? "Uploading in background..."
        : $"Upload {_pendingFiles.Count} File(s)";

    protected override async Task OnInitializedAsync()
    {
        // Subscribe to queue changes so we can update local state + refresh list
        UploadQueue.OnChanged += OnQueueChanged;
        UploadQueue.OnJobCompleted += OnJobCompleted;
        await LoadDocumentsAsync();
    }
    private void ZoomIn()
    {
        _zoomPercent = Math.Min(300, _zoomPercent + 25);
        StateHasChanged();
    }

    private void ZoomOut()
    {
        _zoomPercent = Math.Max(50, _zoomPercent - 25);
        StateHasChanged();
    }

    private void ResetZoom()
    {
        _zoomPercent = 100;
        StateHasChanged();
    }
    // ── Called whenever queue state changes ───────────────────────────────────
    private void OnQueueChanged()
    {
        // Update local "is uploading" flags based on jobs for this beneficiary
        _isCameraJobActive = _lastCameraJobId.HasValue &&
            UploadQueue.IsJobActive(_lastCameraJobId.Value);

        _isPdfJobActive = _lastPdfJobId.HasValue &&
            UploadQueue.IsJobActive(_lastPdfJobId.Value);

        InvokeAsync(StateHasChanged);
    }

    // ── Called by queue service when a job for THIS beneficiary succeeds ──────
    private async void OnJobCompleted(Guid beneficiaryId, string uploadedFileName)
    {
        if (beneficiaryId != EditId) return;

        // Refresh the document list so the new PDF appears immediately
        await InvokeAsync(async () =>
        {
            await LoadDocumentsAsync();
            StateHasChanged();
        });
    }
    private async Task SelectDocumentAsync(BeneficiaryDocumentDto doc)
    {
        if (_selectedDoc?.Id == doc.Id)
        {
            await CloseViewerAsync();
            return;
        }

        _selectedDoc = doc;
        _zoomPercent = 100; // ✅ reset so each newly opened doc starts at default zoom
        var previousUrl = _blobUrl;
        _blobUrl = null;
        StateHasChanged();

        if (!string.IsNullOrEmpty(previousUrl))
        {
            await Task.Delay(500);
            try { await JS.InvokeVoidAsync("revokeBlobUrl", previousUrl); } catch { }
        }

        try
        {
            var bytes = await Http.GetByteArrayAsync(
                $"{ApiBase}/api/beneficiary-documents/stream/{doc.Id}");
            _blobUrl = await JS.InvokeAsync<string>(
                "createPdfBlobUrl", Convert.ToBase64String(bytes));
        }
        catch (Exception ex)
        {
            Messenger.AddError($"Failed to load preview: {ex.Message}");
            _selectedDoc = null;
        }

        StateHasChanged();
    }

    private async Task ExecuteDeleteAsync()
    {
        if (_docToDelete is null) return;
        try
        {
            _isDeleting = true;
            StateHasChanged();

            var response = await Http.DeleteAsync(
                $"{ApiBase}/api/beneficiary-documents/delete/{_docToDelete.Id}?psgcCodeMunicipality={PsgcCodeMunicipality}");

            if (response.IsSuccessStatusCode)
            {
                Messenger.AddInformation("Document deleted successfully.");
                if (_selectedDoc?.Id == _docToDelete.Id)
                    await CloseViewerAsync();
                _docToDelete = null;
                await LoadDocumentsAsync();
                await OnDeleteComplete.InvokeAsync();
            }
            else
            {
                Messenger.AddError("Failed to delete document.");
            }
        }
        catch (Exception ex)
        {
            Messenger.AddError($"Delete error: {ex.Message}");
        }
        finally
        {
            _isDeleting = false;
            StateHasChanged();
        }
    }

    private Task ConfirmDeleteAsync(BeneficiaryDocumentDto doc)
    {
        if (doc == null || doc.Id == Guid.Empty) return Task.CompletedTask;
        _docToRename = null; // ✅ close Rename if it was open on any row
        _docToDelete = doc;
        StateHasChanged();
        return Task.CompletedTask;
    }

    private void CancelDelete()
    {
        _docToDelete = null;
        StateHasChanged();
    }
    private Task OpenRenameModal(BeneficiaryDocumentDto doc)
    {
        _docToDelete = null; // ✅ close Delete if it was open on any row
        _docToRename = doc;
        _renameValue = doc.OriginalFileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)
            ? doc.OriginalFileName[..^4]
            : doc.OriginalFileName;
        _renameError = null;
        StateHasChanged();
        return Task.CompletedTask;
    }

    private void CancelRename()
    {
        _docToRename = null;
        _renameError = null;
        StateHasChanged();
    }

    private async Task ConfirmRenameAsync()
    {
        if (_docToRename is null) return;

        if (string.IsNullOrWhiteSpace(_renameValue))
        {
            _renameError = "File name cannot be empty.";
            return;
        }

        try
        {
            _isRenaming = true;
            _renameError = null;
            StateHasChanged();

            var dto = new RenameDocumentDto { NewFileName = _renameValue.Trim() };
            var response = await Http.PutAsJsonAsync(
                $"{ApiBase}/api/beneficiary-documents/rename/{_docToRename.Id}", dto);

            if (response.IsSuccessStatusCode)
            {
                Messenger.AddInformation("Document renamed successfully.");
                _docToRename = null;
                await LoadDocumentsAsync();
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync();
                _renameError = string.IsNullOrWhiteSpace(error) ? "Rename failed." : error;
            }
        }
        catch (Exception ex)
        {
            _renameError = $"Rename failed: {ex.Message}";
        }
        finally
        {
            _isRenaming = false;
            StateHasChanged();
        }
    }

    private async Task LoadDocumentsAsync()
    {
        try
        {
            _isLoading = true;
            StateHasChanged();
            _documents = await Http.GetFromJsonAsync<List<BeneficiaryDocumentDto>>(
                $"{ApiBase}/api/beneficiary-documents/{EditId}") ?? new();
        }
        catch (Exception ex)
        {
            Messenger.AddError($"Failed to load documents: {ex.Message}");
        }
        finally
        {
            _isLoading = false;
            StateHasChanged();
        }
    }

    private async Task OnFilesChanged(InputFileChangeEventArgs e)
    {
        _fileValidationError = null;
        var invalidFiles = new List<string>();

        foreach (var file in e.GetMultipleFiles(10))
        {
            var isValidMime = file.ContentType == "application/pdf";
            var isValidExt = Path.GetExtension(file.Name)
                .Equals(".pdf", StringComparison.OrdinalIgnoreCase);

            if (!isValidMime || !isValidExt)
            {
                invalidFiles.Add(file.Name);
                continue;
            }
            _pendingFiles.Add(file);
        }

        if (invalidFiles.Any())
            _fileValidationError = $"Only PDF files are allowed. " +
                $"The following file(s) were rejected: {string.Join(", ", invalidFiles)}";

        StateHasChanged();
    }

    private void RemovePending(IBrowserFile file)
    {
        _pendingFiles.Remove(file);
        StateHasChanged();
    }

    private async Task UploadPendingAsync()
    {
        if (!_pendingFiles.Any()) return;
        _fileValidationError = null;

        var payloads = new List<UploadFilePayload>();
        foreach (var file in _pendingFiles)
        {
            using var ms = new MemoryStream();
            await file.OpenReadStream(209_715_200).CopyToAsync(ms);
            payloads.Add(new UploadFilePayload
            {
                Bytes = ms.ToArray(),
                FileName = file.Name,
                ContentType = "application/pdf"
            });
        }

        var jobId = await UploadQueue.EnqueuePdfUploadAsync(EditId, payloads);
        _lastPdfJobId = jobId;
        _isPdfJobActive = true;

        _pendingFiles.Clear();
        Messenger.AddInformation("File(s) queued — uploading in background.");
        StateHasChanged();
    }

    private void SelectDocument(BeneficiaryDocumentDto doc)
    {
        _selectedDoc = _selectedDoc?.Id == doc.Id ? null : doc;
        StateHasChanged();
    }

    private async Task CloseViewerAsync()
    {
        _selectedDoc = null;
        var urlToRevoke = _blobUrl;
        _blobUrl = null;
        StateHasChanged();

        if (!string.IsNullOrEmpty(urlToRevoke))
        {
            await Task.Delay(500);
            try { await JS.InvokeVoidAsync("revokeBlobUrl", urlToRevoke); } catch { }
        }
    }

    private async Task DownloadAsync(BeneficiaryDocumentDto doc)
    {
        try
        {
            var token = await JS.InvokeAsync<string>("localStorage.getItem", "authToken");
            await JS.InvokeVoidAsync("triggerFileDownload",
                $"{ApiBase}/api/beneficiary-documents/download/{doc.Id}",
                doc.OriginalFileName,
                token);
        }
        catch (Exception ex)
        {
            Messenger.AddError($"Download failed: {ex.Message}");
        }
    }

    private string GetStreamUrl(Guid documentId)
        => $"{ApiBase}/api/beneficiary-documents/stream/{documentId}?t={DateTime.UtcNow.Ticks}";

    private static string FormatSize(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1048576 => $"{bytes / 1024.0:F1} KB",
        _ => $"{bytes / 1048576.0:F1} MB"
    };

    // ── Camera capture ────────────────────────────────────────────────────────
    private class CapturedPhoto
    {
        public byte[] Bytes { get; set; } = Array.Empty<byte>();
        public string FileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public string PreviewUrl { get; set; } = string.Empty;
    }

    private List<CapturedPhoto> _capturedPhotos = new();

    private async Task OnCameraPhotosChanged(InputFileChangeEventArgs e)
    {
        foreach (var file in e.GetMultipleFiles(10))
        {
            try
            {
                using var ms = new MemoryStream();
                await file.OpenReadStream(20_000_000).CopyToAsync(ms);
                var bytes = ms.ToArray();
                var base64 = Convert.ToBase64String(bytes);
                var previewUrl = $"data:{file.ContentType};base64,{base64}";

                _capturedPhotos.Add(new CapturedPhoto
                {
                    Bytes = bytes,
                    FileName = file.Name,
                    ContentType = file.ContentType,
                    PreviewUrl = previewUrl
                });
            }
            catch (Exception ex)
            {
                Messenger.AddError($"Failed to load photo '{file.Name}': {ex.Message}");
            }
        }
        StateHasChanged();
    }

    private void RemoveCapturedPhoto(CapturedPhoto photo)
    {
        _capturedPhotos.Remove(photo);
        StateHasChanged();
    }

    private async Task ConfirmCameraUploadAsync()
    {
        if (!_capturedPhotos.Any()) return;

        var payloads = _capturedPhotos.Select(p => new UploadFilePayload
        {
            Bytes = p.Bytes,
            FileName = p.FileName,
            ContentType = p.ContentType
        }).ToList();

        var jobId = await UploadQueue.EnqueueCameraUploadAsync(EditId, payloads);
        _lastCameraJobId = jobId;
        _isCameraJobActive = true;

        _capturedPhotos.Clear();
        Messenger.AddInformation("Photo(s) queued — uploading in background.");
        StateHasChanged();
    }

    public void Dispose()
    {
        UploadQueue.OnChanged -= OnQueueChanged;
        UploadQueue.OnJobCompleted -= OnJobCompleted;
    }
}