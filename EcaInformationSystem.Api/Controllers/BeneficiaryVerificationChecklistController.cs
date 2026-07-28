using EcaInformationSystem.Application.Interfaces;
using EcaInformationSystem.Application.Interfaces.Services;
using EcaInformationSystem.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace EcaInformationSystem.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class BeneficiaryVerificationChecklistController : ControllerBase
    {
        private readonly IBeneficiaryVerificationChecklistService _service;
        private readonly IBeneficiaryInformationService _beneficiaryService; // ✅ NEW

        public BeneficiaryVerificationChecklistController(
            IBeneficiaryVerificationChecklistService service,
            IBeneficiaryInformationService beneficiaryService)
        {
            _service = service;
            _beneficiaryService = beneficiaryService;
        }

        [HttpGet("{beneficiaryId:guid}")]
        public async Task<IActionResult> GetByBeneficiaryId(Guid beneficiaryId)
        {
            var result = await _service.GetByBeneficiaryIdAsync(beneficiaryId);

            // ✅ FIX — never return a bare null. ASP.NET Core's built-in
            // HttpNoContentOutputFormatter turns Ok(null) into a 200 response with a
            // completely EMPTY body (not the literal 4-byte string "null"), which
            // breaks GetFromJsonAsync<T> on the client with a JsonException
            // ("ExpectedJsonTokens") since there's nothing to parse. Returning a
            // fresh, empty DTO instead guarantees the response body is always valid,
            // parseable JSON — and it also matches what the Razor component actually
            // wants on first load (a blank checklist form), so no client-side change
            // is needed either.
            return Ok(result ?? new BeneficiaryVerificationChecklistDto());
        }

        [HttpPost("{beneficiaryId:guid}")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<IActionResult> Upsert(Guid beneficiaryId, [FromBody] BeneficiaryVerificationChecklistDto dto)
        {
            if (dto == null)
                return BadRequest(new { message = "Request body is required." });

            // ✅ NEW — cheap existence check before we ever touch the checklist
            // table. GetByIdAsync already excludes soft-deleted rows (per your
            // repository's !x.IsDeleted filter), so this also correctly rejects
            // a checklist attempt against a deleted beneficiary, not just a
            // nonexistent GUID. Cleaner 404 instead of a raw FK-violation
            // exception bubbling up from SaveChangesAsync.
            var beneficiary = await _beneficiaryService.GetByIdAsync(beneficiaryId);
            if (beneficiary == null)
                return NotFound(new { message = "Beneficiary record not found." });

            var userName = User.FindFirst(ClaimTypes.Name)?.Value;
            if (string.IsNullOrWhiteSpace(userName))
                return Unauthorized(new { message = "Unable to resolve current user." });

            var (success, error) = await _service.UpsertAsync(beneficiaryId, dto, userName);

            if (!success)
                return BadRequest(new { message = error ?? "Failed to save verification checklist." });

            return Ok(new { message = "Verification checklist saved." });
        }
    }
}