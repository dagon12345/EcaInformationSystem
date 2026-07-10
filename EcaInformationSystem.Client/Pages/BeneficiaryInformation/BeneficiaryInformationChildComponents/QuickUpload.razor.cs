using EcaInformationSystem.Client.Services;
using EcaInformationSystem.Shared.DTOs;
using Havit.Blazor.Components.Web;
using Havit.Blazor.Components.Web.Bootstrap;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;

namespace EcaInformationSystem.Client.Pages.BeneficiaryInformation.BeneficiaryInformationChildComponents;

public partial class QuickUpload
{
    [Parameter] public Guid BeneficiaryId { get; set; }
    [Parameter] public int PsgcCodeMunicipality { get; set; }
    [Parameter] public string? LastName { get; set; }
    [Parameter] public string? FirstName { get; set; }
    [Parameter] public EventCallback OnQueued { get; set; }

    [Inject] private DocumentUploadQueueService UploadQueue { get; set; } = default!;
    [Inject] private IHxMessengerService Messenger { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private bool _showPopover;
    private HxModal _webcamModal = default!;
    private readonly string _videoElementId = $"webcam-video-{Guid.NewGuid():N}";
    private readonly string _canvasElementId = $"webcam-canvas-{Guid.NewGuid():N}";
    private bool _isCapturing;
    private string? _webcamError;

    private HxModal _namingModal = default!;
    private string _pendingLabel = string.Empty;
    private List<UploadFilePayload>? _pendingPayloads;
    private bool _pendingIsCameraJob;

    private void TogglePopover() => _showPopover = !_showPopover;
    private void ClosePopover() => _showPopover = false;

    private string GranteeName
    {
        get
        {
            var last = LastName?.Trim();
            var first = FirstName?.Trim();
            if (string.IsNullOrWhiteSpace(last) && string.IsNullOrWhiteSpace(first))
                return "Unnamed Grantee";
            if (string.IsNullOrWhiteSpace(last)) return first!;
            if (string.IsNullOrWhiteSpace(first)) return last!;
            return $"{last}, {first}";
        }
    }

    private string BuildFinalFileName()
    {
        var label = _pendingLabel.Trim();
        var baseName = string.IsNullOrWhiteSpace(label) ? GranteeName : $"{GranteeName} - {label}";
        foreach (var c in Path.GetInvalidFileNameChars())
            baseName = baseName.Replace(c, '-');
        return baseName;
    }

    private string BuildPreviewFileName() => $"{BuildFinalFileName()}.pdf";

    private async Task OnImageFileChanged(InputFileChangeEventArgs e)
    {
        ClosePopover();
        var payloads = new List<UploadFilePayload>();

        foreach (var file in e.GetMultipleFiles(10))
        {
            try
            {
                using var ms = new MemoryStream();
                await file.OpenReadStream(20_000_000).CopyToAsync(ms);
                payloads.Add(new UploadFilePayload
                {
                    Bytes = ms.ToArray(),
                    FileName = file.Name,
                    ContentType = file.ContentType
                });
            }
            catch (Exception ex)
            {
                Messenger.AddError($"Failed to read '{file.Name}': {ex.Message}");
            }
        }

        if (!payloads.Any()) return;

        await PromptForNameAsync(payloads, isCameraJob: true);
    }

    private async Task OnPdfFileChanged(InputFileChangeEventArgs e)
    {
        ClosePopover();
        var payloads = new List<UploadFilePayload>();
        var rejected = new List<string>();

        foreach (var file in e.GetMultipleFiles(10))
        {
            var isValidMime = file.ContentType == "application/pdf";
            var isValidExt = Path.GetExtension(file.Name).Equals(".pdf", StringComparison.OrdinalIgnoreCase);
            if (!isValidMime || !isValidExt)
            {
                rejected.Add(file.Name);
                continue;
            }
            using var ms = new MemoryStream();
            await file.OpenReadStream(209_715_200).CopyToAsync(ms);
            payloads.Add(new UploadFilePayload
            {
                Bytes = ms.ToArray(),
                FileName = file.Name,
                ContentType = "application/pdf"
            });
        }

        if (rejected.Any())
            Messenger.AddWarning($"Only PDF files are allowed. Rejected: {string.Join(", ", rejected)}");

        if (!payloads.Any()) return;

        await PromptForNameAsync(payloads, isCameraJob: false);
    }

    private async Task OpenWebcamModal()
    {
        ClosePopover();
        _webcamError = null;
        await _webcamModal.ShowAsync();

        await Task.Delay(150);
        var started = await JS.InvokeAsync<bool>("webcamInterop.start", _videoElementId);
        if (!started)
        {
            _webcamError = "Could not access your webcam. Check browser permissions, or use " +
                            "\"Choose from Gallery\" instead.";
            StateHasChanged();
        }
    }

    private async Task CaptureWebcamPhotoAsync()
    {
        try
        {
            _isCapturing = true;
            StateHasChanged();

            var dataUrl = await JS.InvokeAsync<string>("webcamInterop.capture", _videoElementId, _canvasElementId);
            if (string.IsNullOrEmpty(dataUrl))
            {
                _webcamError = "Capture failed — please try again.";
                return;
            }

            var base64 = dataUrl[(dataUrl.IndexOf(",") + 1)..];
            var bytes = Convert.FromBase64String(base64);

            var payload = new UploadFilePayload
            {
                Bytes = bytes,
                FileName = $"webcam-{DateTime.Now:yyyyMMdd-HHmmss}.jpg",
                ContentType = "image/jpeg"
            };

            await CloseWebcamModalAsync();
            await PromptForNameAsync(new List<UploadFilePayload> { payload }, isCameraJob: true);
        }
        finally
        {
            _isCapturing = false;
            StateHasChanged();
        }
    }

    private async Task CloseWebcamModalAsync()
    {
        await JS.InvokeVoidAsync("webcamInterop.stop");
        await _webcamModal.HideAsync();
    }

    private async Task PromptForNameAsync(List<UploadFilePayload> payloads, bool isCameraJob)
    {
        _pendingPayloads = payloads;
        _pendingIsCameraJob = isCameraJob;
        _pendingLabel = string.Empty;
        await _namingModal.ShowAsync();
    }

    private async Task ConfirmNamingAsync()
    {
        if (_pendingPayloads is null) return;

        var finalName = $"{BuildFinalFileName()}.pdf"; // ✅ fix — the actual upload
        // filename needs the extension too, not just the preview text. Without
        // it, the server's "is not a PDF file" check rejects every upload that
        // goes through this naming step.
        foreach (var p in _pendingPayloads)
            p.FileName = finalName;

        if (_pendingIsCameraJob)
            await UploadQueue.EnqueueCameraUploadAsync(BeneficiaryId, _pendingPayloads);
        else
            await UploadQueue.EnqueuePdfUploadAsync(BeneficiaryId, _pendingPayloads);

        Messenger.AddInformation($"\"{finalName}.pdf\" queued — uploading in background.");
        _pendingPayloads = null;
        await _namingModal.HideAsync();
        await OnQueued.InvokeAsync();
    }

    private async Task CancelNaming()
    {
        _pendingPayloads = null;
        await _namingModal.HideAsync();
    }
}