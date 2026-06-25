using System.Net.Http.Json;
using EcaInformationSystem.Shared.DTOs;
using Havit.Blazor.Components.Web;
using Havit.Blazor.Components.Web.Bootstrap;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;

namespace EcaInformationSystem.Client.Pages.BeneficiaryInformation
    .BeneficiaryInformationChildComponents;

public partial class BeneficiaryDocumentUpload
{
    [Parameter] public Guid EditId { get; set; }

    [Inject] private HttpClient Http { get; set; } = default!;
    [Inject] private IHxMessengerService Messenger { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    // ✅ Add this — reads the API base URL from config (same source as Program.cs)
    [Inject] private IConfiguration Configuration { get; set; } = default!;
    // ✅ REMOVE this — no longer needed
    // [Parameter] public EventCallback OnRequestHideOffcanvas { get; set; }

    // ✅ KEEP this — still useful to reopen offcanvas after delete
    [Parameter] public EventCallback OnDeleteComplete { get; set; }

    private HxInputFileDropZone _dropZone = default!;
    private bool _isDeleting;
    private List<BeneficiaryDocumentDto> _documents = new();
    private BeneficiaryDocumentDto? _selectedDoc;
    private BeneficiaryDocumentDto? _docToDelete;
    private List<IBrowserFile> _pendingFiles = new();
    private bool _isLoading;
    private bool _isUploading;
    // Add this parameter so the component can hide the offcanvas
    // Actually, simpler — just hide the modal backdrop via JS, or
    // better: emit an event to the parent to hide the offcanvas

    [Parameter] public EventCallback OnRequestHideOffcanvas { get; set; }
    [Parameter] public int PsgcCodeMunicipality { get; set; }
    // ✅ Add this to read the JWT token
    [Inject] private AuthenticationStateProvider AuthStateProvider { get; set; } = default!;
    // ✅ Stores the blob URL for the iframe so it can be revoked later
    private string? _blobUrl;
    private string? _fileValidationError;

    private string ApiBase =>
        (Http.BaseAddress?.ToString() ?? Configuration["ApiBaseUrl"] ?? "https://REDACTED_INTERNAL_IP:8080")
        .TrimEnd('/');

    private string UploadButtonText => _isUploading
        ? "Uploading & Compressing..."
        : $"Upload {_pendingFiles.Count} File(s)";

    protected override async Task OnInitializedAsync()
        => await LoadDocumentsAsync();


    private async Task<string?> GetTokenAsync()
    {
        return await JS.InvokeAsync<string>("localStorage.getItem", "authToken");
    }
    private async Task SelectDocumentAsync(BeneficiaryDocumentDto doc)
    {
        if (_selectedDoc?.Id == doc.Id)
        {
            await CloseViewerAsync();
            return;
        }

        _selectedDoc = doc;

        // ✅ Revoke previous blob URL before loading new one
        var previousUrl = _blobUrl;
        _blobUrl = null;
        StateHasChanged();

        if (!string.IsNullOrEmpty(previousUrl))
        {
            await Task.Delay(500);
            try
            {
                await JS.InvokeVoidAsync("revokeBlobUrl", previousUrl);
            }
            catch { }
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
        if (doc == null || doc.Id == Guid.Empty)
            return Task.CompletedTask;

        _docToDelete = doc;
        StateHasChanged();
        return Task.CompletedTask;
    }
    private void CancelDelete()
    {
        _docToDelete = null;
        StateHasChanged();
    }
    private async Task LoadDocumentsAsync()
    {
        try
        {
            _isLoading = true;
            StateHasChanged();

            // ✅ Use absolute URL
            _documents = await Http.GetFromJsonAsync<List<BeneficiaryDocumentDto>>(
                $"{ApiBase}/api/beneficiary-documents/{EditId}") ?? new();

            foreach (var doc in _documents)
                Console.WriteLine($"DOC: {doc.OriginalFileName} | {doc.Id}");
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
        _fileValidationError = null; // ✅ clear previous error

        var invalidFiles = new List<string>();

        foreach (var file in e.GetMultipleFiles(10))
        {
            // ✅ Validate by MIME type AND extension
            var isValidMime = file.ContentType == "application/pdf";
            var isValidExt = Path.GetExtension(file.Name)
                .Equals(".pdf", StringComparison.OrdinalIgnoreCase);

            if (!isValidMime || !isValidExt)
            {
                invalidFiles.Add(file.Name);
                continue; // skip invalid files
            }

            _pendingFiles.Add(file);
        }

        if (invalidFiles.Any())
        {
            _fileValidationError = $"Only PDF files are allowed. " +
                $"The following file(s) were rejected: {string.Join(", ", invalidFiles)}";
        }

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
        try
        {
            _isUploading = true;
            StateHasChanged();

            using var content = new MultipartFormDataContent();
            foreach (var file in _pendingFiles)
            {
                var stream = file.OpenReadStream(209_715_200);
                var fileContent = new StreamContent(stream);
                fileContent.Headers.ContentType =
                    new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");
                content.Add(fileContent, "files", file.Name);
            }

            // ✅ Use absolute URL
            var response = await Http.PostAsync(
                $"{ApiBase}/api/beneficiary-documents/{EditId}/upload", content);

            if (response.IsSuccessStatusCode)
            {
                Messenger.AddInformation(
                    $"{_pendingFiles.Count} document(s) uploaded successfully.");
                _pendingFiles.Clear();
                await LoadDocumentsAsync();
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync();
                Messenger.AddError($"Upload failed: {error}");
            }
        }
        catch (Exception ex)
        {
            Messenger.AddError($"Upload error: {ex.Message}");
        }
        finally
        {
            _isUploading = false;
        }
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
        StateHasChanged(); // ✅ re-render first — iframe is removed from DOM

        // ✅ Delay revocation so the iframe fully unloads before blob is freed
        // This prevents the debugger disconnect in local dev
        if (!string.IsNullOrEmpty(urlToRevoke))
        {
            await Task.Delay(500);
            try
            {
                await JS.InvokeVoidAsync("revokeBlobUrl", urlToRevoke);
            }
            catch
            {
                // ✅ Silently ignore — component may have been disposed
                // between the delay and the revoke call
            }
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
                token);  // ✅ pass token
        }
        catch (Exception ex)
        {
            Messenger.AddError($"Download failed: {ex.Message}");
        }
    }
    // ✅ Now returns an absolute URL — iframe won't hit the Blazor router
    private string GetStreamUrl(Guid documentId)
    {
        return $"{ApiBase}/api/beneficiary-documents/stream/{documentId}?t={DateTime.UtcNow.Ticks}";
    }

    private static string FormatSize(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1048576 => $"{bytes / 1024.0:F1} KB",
        _ => $"{bytes / 1048576.0:F1} MB"
    };
}