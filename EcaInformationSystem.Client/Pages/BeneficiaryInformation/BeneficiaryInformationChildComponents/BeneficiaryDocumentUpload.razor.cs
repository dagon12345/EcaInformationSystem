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
    [Parameter] public string? LastName { get; set; }
    [Parameter] public string? FirstName { get; set; }

    [Inject] private AuthenticationStateProvider AuthStateProvider { get; set; } = default!;

    private bool _isDeleting;
    private BeneficiaryDocumentDto? _docToRename;
    private string _renameValue = string.Empty;
    private string? _renameError;
    private bool _isRenaming;
    private List<BeneficiaryDocumentDto> _documents = new();
    private BeneficiaryDocumentDto? _selectedDoc;
    private BeneficiaryDocumentDto? _docToDelete;
    private bool _isLoading;
    private string? _blobUrl;
    private int _zoomPercent = 100;
    private string ViewerSrc => $"{_blobUrl}#zoom={_zoomPercent}";

    private string ApiBase =>
        (Http.BaseAddress?.ToString() ?? Configuration["ApiBaseUrl"] ?? "https://REDACTED_INTERNAL_IP:8080")
        .TrimEnd('/');

    protected override async Task OnInitializedAsync()
    {
        // All uploads now go through the shared QuickUpload component — this
        // panel only needs to know when one finishes, to refresh the list.
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

    public void Dispose()
    {
        UploadQueue.OnJobCompleted -= OnJobCompleted;
    }
}