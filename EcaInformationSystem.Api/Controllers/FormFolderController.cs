using EcaInformationSystem.Application.Interfaces.Services;
using EcaInformationSystem.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace EcaInformationSystem.Api.Controllers
{
    [ApiController]
    [Route("api/formfolder")]
    [Authorize(Policy = AuthPolicies.CookieOrJwt)]
    public class FormFolderController : ControllerBase
    {
        private readonly IFormFolderService _service;

        public FormFolderController(IFormFolderService service)
        {
            _service = service;
        }

        private string CurrentUser =>
            User.FindFirst(ClaimTypes.Name)?.Value ?? User.FindFirst("sub")?.Value ?? "Unknown";

        // ── Everyone can see the folder list (for browsing/filtering) ──────────
        [HttpGet]
        public async Task<ActionResult<List<FormFolderDto>>> GetAll()
            => Ok(await _service.GetAllAsync());

        // ── Admin / SuperAdmin / Viewer — Viewer has full access in Forms Gateway ──
        [HttpPost]
        [Authorize(Policy = "AdminOrViewer")]
        public async Task<ActionResult<FormFolderDto>> Create([FromBody] FormFolderCreateDto dto)
        {
            try { return Ok(await _service.CreateAsync(dto, CurrentUser)); }
            catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
        }

        [HttpPut("{id:guid}")]
        [Authorize(Policy = "AdminOrViewer")]
        public async Task<IActionResult> Update(Guid id, [FromBody] FormFolderUpdateDto dto)
        {
            try
            {
                await _service.UpdateAsync(id, dto, CurrentUser);
                return NoContent();
            }
            catch (KeyNotFoundException) { return NotFound(); }
            catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
        }

        [HttpDelete("{id:guid}")]
        [Authorize(Policy = "AdminOrViewer")]
        public async Task<ActionResult<int>> Delete(Guid id)
        {
            try { return Ok(await _service.DeleteAsync(id, CurrentUser)); }
            catch (KeyNotFoundException) { return NotFound(); }
        }

        [HttpGet("activity-log")]
        [Authorize(Policy = "AdminOrViewer")]
        public async Task<ActionResult<List<FormActivityLogDto>>> GetActivityLog([FromQuery] int take = 100)
            => Ok(await _service.GetActivityLogAsync(take));
    }
}