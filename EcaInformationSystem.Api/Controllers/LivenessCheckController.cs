using EcaInformationSystem.Api.Extensions;
using EcaInformationSystem.Application.Interfaces.Services;
using EcaInformationSystem.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EcaInformationSystem.Api.Controllers
{
    [ApiController]
    [Route("api/liveness-check")]
    public class LivenessCheckController : ControllerBase
    {
        private readonly ILivenessCheckService _service;
        private readonly IConfiguration _configuration;

        public LivenessCheckController(ILivenessCheckService service, IConfiguration configuration)
        {
            _service = service;
            _configuration = configuration;
        }

        // ── Grantee-facing, no auth — reachable only via the random token ──────────

        [HttpGet("{token}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetPublicView(string token)
        {
            var dto = await _service.GetPublicViewAsync(token);
            if (dto is null)
                return NotFound("This link is no longer valid. Please ask your PDO for a new one.");

            return Ok(dto);
        }

        [HttpPost("{token}/submit")]
        [AllowAnonymous]
        [RequestSizeLimit(1_048_576)] // 1MB — well above the ~100KB target, still tiny
        public async Task<IActionResult> SubmitPhoto(string token, [FromBody] LivenessSubmitRequestDto dto)
        {
            try
            {
                await _service.SubmitPhotoAsync(token, dto);
                return Ok();
            }
            catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
        }

        // ── PDO-facing, requires the normal JWT auth ────────────────────────────────

        [HttpPost("generate/{beneficiaryId:guid}")]
        [Authorize]
        public async Task<IActionResult> GenerateLink(Guid beneficiaryId)
        {
            try
            {
                var userId = User.Identity?.Name ?? "System";
                var role = User.GetRole();
                var result = await _service.GenerateLinkAsync(beneficiaryId, userId, role, GetClientBaseUrl());
                return Ok(result);
            }
            catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
        }

        [HttpGet("history/{beneficiaryId:guid}")]
        [Authorize]
        public async Task<IActionResult> GetHistory(Guid beneficiaryId)
        {
            var history = await _service.GetHistoryAsync(beneficiaryId, GetClientBaseUrl());
            return Ok(history);
        }

        // Grantees whose submitted photo is still awaiting review — scoped to
        // the caller's role/jurisdiction. Backs the notification bell's
        // "Liveness" tab (both its initial load and its badge count).
        [HttpGet("pending-reviews")]
        [Authorize]
        public async Task<IActionResult> GetPendingReviews()
        {
            var userName = User.Identity?.Name ?? "";
            var role = User.GetRole();
            var items = await _service.GetPendingReviewsForUserAsync(userName, role);
            return Ok(items);
        }

        [HttpGet("{recordId:guid}/photo")]
        [Authorize]
        public async Task<IActionResult> GetPhoto(Guid recordId)
        {
            try
            {
                var (bytes, contentType) = await _service.GetPhotoAsync(recordId);
                return File(bytes, contentType);
            }
            catch (InvalidOperationException ex) { return NotFound(ex.Message); }
        }

        [HttpPost("{recordId:guid}/verify")]
        [Authorize]
        public async Task<IActionResult> Verify(Guid recordId, [FromBody] LivenessReviewRequestDto? dto)
        {
            try
            {
                var userId = User.Identity?.Name ?? "System";
                await _service.VerifyAsync(recordId, userId, dto?.Notes);
                return NoContent();
            }
            catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
        }

        [HttpPost("{recordId:guid}/reject")]
        [Authorize]
        public async Task<IActionResult> Reject(Guid recordId, [FromBody] LivenessReviewRequestDto? dto)
        {
            try
            {
                var userId = User.Identity?.Name ?? "System";
                await _service.RejectAsync(recordId, userId, dto?.Notes);
                return NoContent();
            }
            catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
        }

        // Deleting a link (rather than just letting it sit inactive/rejected)
        // permanently removes that attempt's row, photo included — restricted
        // to SuperAdmin.
        [HttpDelete("{recordId:guid}")]
        [Authorize(Policy = "SuperAdminOnly")]
        public async Task<IActionResult> Delete(Guid recordId)
        {
            try
            {
                var userName = User.Identity?.Name ?? "System";
                await _service.DeleteLinkAsync(recordId, userName);
                return NoContent();
            }
            catch (InvalidOperationException ex) { return NotFound(ex.Message); }
        }

        // The public /liveness/{token} route lives in the separate Blazor Client
        // app, not this API — Request.Host would point at the API's own origin.
        // Reuse the WasmOrigin config already used for CORS.
        private string GetClientBaseUrl() => _configuration["Cors:WasmOrigin"] ?? "https://REDACTED_INTERNAL_IP";
    }
}
