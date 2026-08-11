using EcaInformationSystem.Application.Interfaces.Services;
using EcaInformationSystem.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace EcaInformationSystem.Api.Controllers
{
    [ApiController]
    [Route("api/formdocument")]
    [Authorize(Policy = AuthPolicies.CookieOrJwt)]
    public class FormDocumentController : ControllerBase
    {
        private readonly IFormDocumentService _service;

        public FormDocumentController(IFormDocumentService service)
        {
            _service = service;
        }

        private string CurrentUser =>
            User.FindFirst(ClaimTypes.Name)?.Value ?? User.FindFirst("sub")?.Value ?? "Unknown";

        [HttpGet]
        public async Task<ActionResult<List<FormDocumentDto>>> GetAll()
            => Ok(await _service.GetAllAsync());

        // ✅ NEW — search endpoint, available to everyone (view/download roles included)
        [HttpPost("search")]
        public async Task<ActionResult<List<FormDocumentDto>>> Search([FromBody] FormDocumentSearchDto filter)
            => Ok(await _service.SearchAsync(filter));

        [HttpGet("{id:guid}/download")]
        public async Task<IActionResult> Download(Guid id)
        {
            var result = await _service.DownloadAsync(id);
            if (result == null) return NotFound();
            return File(result.Value.Data, result.Value.ContentType, result.Value.FileName);
        }

        // Lets the client show "estimated result: X MB" for a chosen shrink
        // quality before the user commits to uploading. The shrunk bytes are
        // cached server-side (keyed by the returned PreviewToken) so the
        // confirming /upload call doesn't have to re-send or re-shrink the file.
        [HttpPost("shrink-preview")]
        [Authorize(Policy = "AdminOrViewer")]
        [RequestSizeLimit(50 * 1024 * 1024)]
        public async Task<ActionResult<ShrinkPreviewResultDto>> ShrinkPreview([FromForm] ShrinkPreviewRequest request)
        {
            try { return Ok(await _service.PreviewShrinkAsync(request.File, request.Quality)); }
            catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
        }

        // 50 MB — the hard ceiling before a shrink is even attempted. Files that
        // pass this but are still over 10 MB get routed through the shrink
        // pipeline inside FormDocumentService; only the final, post-shrink bytes
        // are ever persisted, so the stored-size limit is unaffected.
        //
        // Accepts EITHER request.File (no shrink needed / not yet previewed) OR
        // request.PreviewToken (result of a prior /shrink-preview call) — never
        // both. The token path avoids re-uploading/re-shrinking the same file.
        [HttpPost("upload")]
        [Authorize(Policy = "AdminOrViewer")]
        [RequestSizeLimit(50 * 1024 * 1024)]
        public async Task<ActionResult<FormDocumentDto>> Upload(
            [FromForm] UploadFormDocumentRequest request)
        {
            try
            {
                var dto = request.PreviewToken.HasValue
                    ? await _service.UploadFromPreviewAsync(
                        request.PreviewToken.Value, request.Title, request.Description,
                        request.Category, request.FolderId, CurrentUser)
                    : await _service.UploadAsync(
                        request.File ?? throw new InvalidOperationException("No file was uploaded."),
                        request.Title, request.Description,
                        request.Category, request.FolderId, request.ShrinkQuality, CurrentUser);
                return Ok(dto);
            }
            catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
            catch (KeyNotFoundException ex) { return BadRequest(ex.Message); }
        }

        [HttpPut("{id:guid}/file")]
        [Authorize(Policy = "AdminOrViewer")]
        [RequestSizeLimit(50 * 1024 * 1024)]
        public async Task<ActionResult<FormDocumentDto>> ReplaceFile(Guid id, [FromForm] ReplaceFileRequest request)
        {
            try { return Ok(await _service.ReplaceFileAsync(id, request.File, request.ShrinkQuality, CurrentUser)); }
            catch (KeyNotFoundException) { return NotFound(); }
            catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
        }

        [HttpPut("{id:guid}")]
        [Authorize(Policy = "AdminOrViewer")]
        public async Task<IActionResult> UpdateMetadata(Guid id, [FromBody] FormDocumentUpdateDto dto)
        {
            try
            {
                await _service.UpdateMetadataAsync(id, dto, CurrentUser);
                return NoContent();
            }
            catch (KeyNotFoundException) { return NotFound(); }
            catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
        }

        [HttpDelete("{id:guid}")]
        [Authorize(Policy = "AdminOrViewer")]
        public async Task<IActionResult> Delete(Guid id)
        {
            try
            {
                await _service.DeleteAsync(id, CurrentUser);
                return NoContent();
            }
            catch (KeyNotFoundException) { return NotFound(); }
        }
    }
}