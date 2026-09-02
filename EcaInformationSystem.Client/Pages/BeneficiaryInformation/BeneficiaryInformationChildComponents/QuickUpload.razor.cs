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
    private readonly string _uploadButtonId = $"quick-upload-btn-{Guid.NewGuid():N}";
    private readonly string _popoverId = $"quick-upload-popover-{Guid.NewGuid():N}";
    private HxModal _webcamModal = default!;
    private readonly string _webcamModalClass = $"quick-upload-webcam-modal-{Guid.NewGuid():N}";
    private readonly string _videoElementId = $"webcam-video-{Guid.NewGuid():N}";
    private readonly string _canvasElementId = $"webcam-canvas-{Guid.NewGuid():N}";
    private bool _isCapturing;
    private string? _webcamError;

    private HxModal _namingModal = default!;
    private readonly string _namingModalClass = $"quick-upload-naming-modal-{Guid.NewGuid():N}";
    private string _pendingLabel = string.Empty;
    private List<UploadFilePayload>? _pendingPayloads;
    private bool _pendingIsCameraJob;
    private bool _stampDateOnPhoto;
    private DateTime _stampDateTimeLocal = DateTime.Now;

    // QuickUpload is reused both on plain grid rows AND nested deep inside the
    // beneficiary details offcanvas (a `position:fixed` + z-index element that
    // establishes its own stacking context). In the offcanvas case, this
    // modal's dialog — despite its own higher CSS z-index — paints as part of
    // that offcanvas's stacking context, which sits BELOW the modal's own
    // backdrop (appended straight to <body> by Bootstrap's native JS, so it's
    // in the root stacking context instead). The backdrop then visually
    // covers the dialog and swallows clicks meant for its buttons. Moving the
    // dialog element itself to be a direct child of <body> (once it's shown)
    // puts it in that same root context, above its own backdrop, fixing both
    // the washed-out look and the unresponsive buttons.
    private async Task RelocateWebcamModalAsync() =>
        await TryRelocateModalAsync(_webcamModalClass);

    private async Task RelocateNamingModalAsync() =>
        await TryRelocateModalAsync(_namingModalClass);

    private async Task TryRelocateModalAsync(string modalClass)
    {
        try { await JS.InvokeVoidAsync("relocateModalToBody", modalClass); }
        catch { /* non-fatal — worst case the modal stays wherever it was rendered */ }
    }

    private void TogglePopover()
    {
        _showPopover = !_showPopover;
        if (_showPopover)
        {
            // Position as a viewport-fixed overlay AFTER the popover has
            // rendered (and only once its real size can be measured) —
            // escapes clipping from any scrollable ancestor (e.g. the
            // offcanvas body), which a plain CSS `position: absolute`
            // popover can't do regardless of z-index.
            _ = InvokeAsync(async () =>
            {
                await Task.Yield();
                try { await JS.InvokeVoidAsync("positionFloatingPopover", _uploadButtonId, _popoverId); }
                catch { /* non-fatal — worst case the popover just isn't repositioned */ }
            });
        }
    }

    private void ClosePopover()
    {
        _showPopover = false;
        // Force the popover + its full-screen backdrop out of the DOM right
        // away — without this, they could still be present (mid-removal)
        // when a modal opens immediately after, and the two full-screen
        // overlays stacking briefly is what produced the "blacked out" look.
        StateHasChanged();
    }

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
        // Do NOT close the popover yet — that removes the <InputFile> element
        // from the DOM, which would tear down the very stream we're still
        // reading from below and silently produce zero payloads.
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

        ClosePopover();

        if (!payloads.Any()) return;

        await PromptForNameAsync(payloads, isCameraJob: true);
    }

    private async Task OnPdfFileChanged(InputFileChangeEventArgs e)
    {
        // Do NOT close the popover yet — see OnImageFileChanged for why.
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

        ClosePopover();

        if (rejected.Any())
            Messenger.AddWarning($"Only PDF files are allowed. Rejected: {string.Join(", ", rejected)}");

        if (!payloads.Any()) return;

        await PromptForNameAsync(payloads, isCameraJob: false);
    }

    private async Task OpenWebcamModal()
    {
        ClosePopover();
        _webcamError = null;
        // Let the popover-closed render actually commit before this modal
        // opens, so the two overlays never briefly coexist.
        await Task.Yield();
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
        _stampDateOnPhoto = false;
        _stampDateTimeLocal = DateTime.Now;
        // Let the popover-closed render (see ClosePopover) actually commit
        // before this modal opens, so the two overlays never briefly coexist.
        await Task.Yield();
        await _namingModal.ShowAsync();
    }

    private async Task ConfirmNamingAsync()
    {
        if (_pendingPayloads is null) return;

        if (_pendingIsCameraJob && _stampDateOnPhoto)
        {
            var timestampText = _stampDateTimeLocal.ToString("MMM d, yyyy h:mm tt");
            foreach (var p in _pendingPayloads)
            {
                try
                {
                    var base64 = Convert.ToBase64String(p.Bytes);
                    var stampedBase64 = await JS.InvokeAsync<string>("stampImageWithDate", base64, p.ContentType, timestampText);
                    p.Bytes = Convert.FromBase64String(stampedBase64);
                }
                catch (Exception ex)
                {
                    Messenger.AddError($"Failed to stamp date on '{p.FileName}': {ex.Message}");
                    return;
                }
            }
        }

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