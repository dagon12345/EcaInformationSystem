// Api/Controllers/UserManagementController.cs
using EcaInformationSystem.Application.Interfaces.Services;
using EcaInformationSystem.Shared.DTOs.UserManagement;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EcaInformationSystem.Api.Controllers
{
    [ApiController]
    [Route("api/user-management")]
    [Authorize(Policy = "SuperAdminOnly")]  // ✅ only SuperAdmin touches this
    public class UserManagementController : ControllerBase
    {
        private readonly IUserManagementService _service;

        public UserManagementController(IUserManagementService service)
        {
            _service = service;
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
    }
}