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

        [HttpPost("upload")]
        [Authorize(Policy = "AdminOnly")]
        [RequestSizeLimit(10 * 1024 * 1024)]
        public async Task<ActionResult<FormDocumentDto>> Upload(
            [FromForm] UploadFormDocumentRequest request)
        {
            try
            {
                var dto = await _service.UploadAsync(
                    request.File, request.Title, request.Description,
                    request.Category, request.FolderId, CurrentUser);
                return Ok(dto);
            }
            catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
            catch (KeyNotFoundException ex) { return BadRequest(ex.Message); }
        }

        [HttpPut("{id:guid}/file")]
        [Authorize(Policy = "AdminOnly")]
        [RequestSizeLimit(10 * 1024 * 1024)]
        public async Task<ActionResult<FormDocumentDto>> ReplaceFile(Guid id, [FromForm] ReplaceFileRequest request)
        {
            try { return Ok(await _service.ReplaceFileAsync(id, request.File, CurrentUser)); }
            catch (KeyNotFoundException) { return NotFound(); }
            catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
        }

        [HttpPut("{id:guid}")]
        [Authorize(Policy = "AdminOnly")]
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
        [Authorize(Policy = "AdminOnly")]
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