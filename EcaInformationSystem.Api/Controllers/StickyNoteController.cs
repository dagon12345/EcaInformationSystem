using EcaInformationSystem.Application.Interfaces.Services;
using EcaInformationSystem.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EcaInformationSystem.Api.Controllers
{
    // Strictly private — every route is scoped to the caller's own user id.
    // There is deliberately no admin or "view other user's notes" route.
    [ApiController]
    [Route("api/stickynotes")]
    [Authorize(Policy = AuthPolicies.CookieOrJwt)]
    public class StickyNoteController : ControllerBase
    {
        private readonly IStickyNoteService _service;

        public StickyNoteController(IStickyNoteService service)
        {
            _service = service;
        }

        private Guid CurrentUserId => Guid.Parse(User.FindFirst("sub")!.Value);

        [HttpGet]
        public async Task<IActionResult> GetMine()
            => Ok(await _service.GetMineAsync(CurrentUserId));

        [HttpPost]
        public async Task<IActionResult> Upsert([FromBody] StickyNoteUpsertDto dto)
        {
            try { return Ok(await _service.UpsertAsync(CurrentUserId, dto)); }
            catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
            catch (KeyNotFoundException) { return NotFound(); }
        }

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            await _service.DeleteAsync(CurrentUserId, id);
            return NoContent();
        }
    }
}
