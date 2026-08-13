using EcaInformationSystem.Api.Hubs;
using EcaInformationSystem.Application.Interfaces.Services;
using EcaInformationSystem.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace EcaInformationSystem.Api.Controllers
{
    // Audit trail for browser-to-browser voice calls. Only SuperAdmin sees
    // every call (and is the only role that may delete one); Admin/PDO/Focal
    // each see only calls they were personally a participant in — enforced
    // both here and again in the service, since a bare [Authorize] mistake
    // shouldn't be the only thing standing between "view" and "delete".
    [ApiController]
    [Route("api/voicecalllog")]
    public class VoiceCallLogController : ControllerBase
    {
        private readonly IVoiceCallLogService _service;
        private readonly IHubContext<VoiceCallHub> _hub;

        public VoiceCallLogController(IVoiceCallLogService service, IHubContext<VoiceCallHub> hub)
        {
            _service = service;
            _hub = hub;
        }

        private Guid CurrentUserId => Guid.Parse(User.FindFirst("sub")!.Value);
        private string CurrentRole => User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? "";

        [HttpGet]
        [Authorize(Policy = "CallLogViewers")]
        public async Task<IActionResult> GetMine()
            => Ok(await _service.GetForViewerAsync(CurrentUserId, CurrentRole));

        [HttpPut("{id:guid}/notes")]
        [Authorize(Policy = "AnyAuthenticatedIncludingFocal")] // a Focal is a valid call participant too
        public async Task<IActionResult> UpdateNotes(Guid id, [FromBody] UpdateVoiceCallLogNotesRequest request)
        {
            try
            {
                var result = await _service.UpdateNotesAsync(id, CurrentUserId, request.Notes);

                // ✅ If this same account is also open on another device (e.g. the
                // call happened on the PC, but the phone is logged in too), that
                // device's post-call notes prompt for this same log — if it's
                // somehow still showing one — should close now that it's saved,
                // rather than sitting there stale/duplicated.
                await _hub.Clients.User(CurrentUserId.ToString()).SendAsync("CallNotesSaved", id);

                return Ok(result);
            }
            catch (KeyNotFoundException) { return NotFound(); }
            catch (UnauthorizedAccessException) { return Forbid(); }
        }

        [HttpDelete("{id:guid}")]
        [Authorize(Policy = "SuperAdminOnly")]
        public async Task<IActionResult> Delete(Guid id)
        {
            await _service.DeleteAsync(id, CurrentRole);
            return NoContent();
        }
    }
}
