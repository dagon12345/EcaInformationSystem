using EcaInformationSystem.Application.Interfaces;
using EcaInformationSystem.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EcaInformationSystem.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class LiquidationController : ControllerBase
    {
        private readonly IBeneficiaryInformationService _service;

        public LiquidationController(IBeneficiaryInformationService service)
            => _service = service;

        [HttpGet("cgp-members")]
        public async Task<IActionResult> GetCgpMembers(
      [FromQuery] Guid cgpGenerationId, [FromQuery] int municipalityCode, [FromQuery] int milestoneYear)
        {
            if (cgpGenerationId == Guid.Empty)
                return BadRequest(new { message = "cgpGenerationId is required." });

            var members = await _service.GetCgpRangeMembersAsync(cgpGenerationId, municipalityCode, milestoneYear);
            return Ok(members);
        }

        /// <summary>Returns the grouped CDR preview rows for the modal table.</summary>
        [HttpPost("preview")]
        public async Task<IActionResult> Preview([FromBody] GenerateCdrRequestDto request)
        {
            try
            {
                // Force paid only
                var rows = await _service.BuildCdrPreviewAsync(request.Filter, request.Settings);
                return Ok(rows);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>Generates and returns the CDR Excel file.</summary>
        [HttpPost("generate-cdr")]
        public async Task<IActionResult> GenerateCdr([FromBody] GenerateCdrRequestDto request)
        {
            try
            {
                // Force paid only
                var bytes = await _service.GenerateCdrAsync(request.Filter, request.Settings);
                return File(
                    bytes,
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    $"CDR_{DateTime.Now:yyyy-MM-dd}.xlsx");
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }

    public class GenerateCdrRequestDto
    {
        public LiquidationFilterDto Filter { get; set; } = new();
        public LiquidationSettingsDto Settings { get; set; } = new();
    }
}