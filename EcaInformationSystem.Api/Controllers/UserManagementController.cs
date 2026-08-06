// Api/Controllers/UserManagementController.cs
using EcaInformationSystem.Application.Interfaces.Services;
using EcaInformationSystem.Shared.DTOs.Auth;
using EcaInformationSystem.Shared.DTOs.UserManagement;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EcaInformationSystem.Api.Controllers
{
    [ApiController]
    [Route("api/user-management")]
    [Authorize(Policy = "SuperAdminOrFinance")]  // ✅ SuperAdmin and Finance have equal access here
    public class UserManagementController : ControllerBase
    {
        private readonly IUserManagementService _service;
        private readonly IPasswordResetService _passwordResetService; // ✅ NEW — add to constructor 
        public UserManagementController(IUserManagementService service, IPasswordResetService passwordResetService)
        {
            _service = service;
            _passwordResetService = passwordResetService;
        }

        // ── List all users ────────────────────────────────────────────────────
        [HttpGet]
        public async Task<IActionResult> GetAll()
            => Ok(await _service.GetAllUsersAsync());

        // ── Get single user ───────────────────────────────────────────────────
        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var user = await _service.GetUserByIdAsync(id);
            return user is null ? NotFound() : Ok(user);
        }

        // ── Approve + assign role ─────────────────────────────────────────────
        [HttpPost("{id:guid}/approve")]
        public async Task<IActionResult> Approve(
            Guid id, [FromBody] ApproveUserRequestDto dto)
        {
            try
            {
                var approvedBy = User.Identity?.Name ?? "SuperAdmin";
                await _service.ApproveAsync(id, dto.Role, dto.Remarks, approvedBy);
                return Ok(new { message = "User approved successfully." });
            }
            catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
            catch (ArgumentException ex) { return BadRequest(ex.Message); }
            catch (Exception ex) { return BadRequest(ex.Message); }
        }

        // ── Reject ────────────────────────────────────────────────────────────
        [HttpPost("{id:guid}/reject")]
        public async Task<IActionResult> Reject(
            Guid id, [FromBody] RejectUserRequestDto dto)
        {
            try
            {
                var rejectedBy = User.Identity?.Name ?? "SuperAdmin";
                await _service.RejectAsync(id, dto.Remarks, rejectedBy);
                return Ok(new { message = "User rejected." });
            }
            catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
            catch (Exception ex) { return BadRequest(ex.Message); }
        }

        // ── Deactivate (reversible login lock — e.g. employee resigned) ───────
        [HttpPost("{id:guid}/deactivate")]
        public async Task<IActionResult> Deactivate(
            Guid id, [FromBody] DeactivateUserRequestDto dto)
        {
            try
            {
                var deactivatedBy = User.Identity?.Name ?? "SuperAdmin";
                await _service.DeactivateAsync(id, dto.Remarks, deactivatedBy);
                return Ok(new { message = "User deactivated." });
            }
            catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
            catch (Exception ex) { return BadRequest(ex.Message); }
        }

        // ── Reactivate ──────────────────────────────────────────────────────
        [HttpPost("{id:guid}/reactivate")]
        public async Task<IActionResult> Reactivate(Guid id)
        {
            try
            {
                var reactivatedBy = User.Identity?.Name ?? "SuperAdmin";
                await _service.ReactivateAsync(id, reactivatedBy);
                return Ok(new { message = "User reactivated." });
            }
            catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
            catch (Exception ex) { return BadRequest(ex.Message); }
        }

        // ── Permanently delete the account ─────────────────────────────────
        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            try
            {
                await _service.DeleteAsync(id);
                return Ok(new { message = "User deleted." });
            }
            catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
            catch (Exception ex) { return BadRequest(ex.Message); }
        }

        // ── Assign PDO jurisdictions ───────────────────────────────────────────
        [HttpPost("{id:guid}/jurisdictions")]
        public async Task<IActionResult> AssignJurisdictions(
            Guid id, [FromBody] AssignJurisdictionRequestDto dto)
        {
            try
            {
                var assignedBy = User.Identity?.Name ?? "SuperAdmin";
                await _service.AssignJurisdictionsAsync(
                    id, dto.MunicipalityCodes, assignedBy);
                return Ok(new { message = "Jurisdictions assigned successfully." });
            }
            catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
            catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
            catch (Exception ex) { return BadRequest(ex.Message); }
        }

        // ── Get jurisdictions for a PDO ────────────────────────────────────────
        [HttpGet("{id:guid}/jurisdictions")]
        public async Task<IActionResult> GetJurisdictions(Guid id)
            => Ok(await _service.GetJurisdictionCodesAsync(id));

        [HttpGet("password-reset-requests")]
        public async Task<IActionResult> GetPasswordResetRequests()
            => Ok(await _passwordResetService.GetAllRequestsAsync());

        [HttpPost("password-reset-requests/{id:guid}/approve")]
        public async Task<IActionResult> ApprovePasswordReset(Guid id, [FromBody] ApprovePasswordResetDto dto)
        {
            try
            {
                var approvedBy = User.Identity?.Name ?? "SuperAdmin";
                var result = await _passwordResetService.ApproveAsync(id, dto.Remarks, approvedBy);
                return Ok(result); // { code, expiresAt } — shown once to the admin
            }
            catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
            catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
        }

        [HttpPost("password-reset-requests/{id:guid}/reject")]
        public async Task<IActionResult> RejectPasswordReset(Guid id, [FromBody] RejectPasswordResetDto dto)
        {
            try
            {
                var rejectedBy = User.Identity?.Name ?? "SuperAdmin";
                await _passwordResetService.RejectAsync(id, dto.Remarks, rejectedBy);
                return Ok(new { message = "Request rejected." });
            }
            catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
            catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
        }
        [HttpGet("password-reset-requests/{id:guid}/view-code")]
        public async Task<IActionResult> ViewPasswordResetCode(Guid id)
        {
            var result = await _passwordResetService.ViewCodeAsync(id);
            if (result == null) return NotFound(new { message = "Code unavailable or expired." });
            return Ok(result);
        }
        [HttpPost("password-reset-requests/{id:guid}/regenerate")]
        public async Task<IActionResult> RegeneratePasswordResetCode(Guid id)
        {
            try
            {
                var regeneratedBy = User.Identity?.Name ?? "SuperAdmin";
                var result = await _passwordResetService.RegenerateCodeAsync(id, regeneratedBy);
                return Ok(result);
            }
            catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
            catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
        }
    }
}