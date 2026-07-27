using EcaInformationSystem.Application.Interfaces.Services;
using EcaInformationSystem.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EcaInformationSystem.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class AnnualGranteeTargetController : ControllerBase
    {
        private readonly IAnnualGranteeTargetService _service;
        private readonly ICurrentUserService _currentUser;

        public AnnualGranteeTargetController(IAnnualGranteeTargetService service, ICurrentUserService currentUser)
        {
            _service = service;
            _currentUser = currentUser;
        }

        // Any authenticated user (Admin/SuperAdmin/PDO/Viewer) can VIEW the
        // target-vs-actual comparison for their own region — only setting the
        // target itself is Admin/SuperAdmin-only, enforced below in Upsert.
        [HttpGet("{fiscalYear:int}")]
        public async Task<IActionResult> Get(int fiscalYear)
        {
            var regionCode = GetRegionCodeFromClaims();
            if (regionCode is null)
                return BadRequest("No region is assigned to this account.");

            var result = await _service.GetComparisonAsync(regionCode.Value, fiscalYear);
            return Ok(result);
        }

        [HttpPut]
        public async Task<IActionResult> Upsert([FromBody] UpsertAnnualGranteeTargetDto dto)
        {
            if (!User.IsInRole("Admin") && !User.IsInRole("SuperAdmin"))
                return Forbid();

            var regionCode = GetRegionCodeFromClaims();
            if (regionCode is null)
                return BadRequest("No region is assigned to this account.");

            if (dto.MonthlyTargets is null || dto.MonthlyTargets.Length != 12)
                return BadRequest("Exactly 12 monthly target values (January-December) are required.");

            if (dto.MonthlyTargets.Any(t => t < 0))
                return BadRequest("Monthly targets cannot be negative.");

            var userName = _currentUser.GetUserName();
            var result = await _service.UpsertAsync(regionCode.Value, dto.FiscalYear, dto.MonthlyTargets, userName);
            return Ok(result);
        }

        private int? GetRegionCodeFromClaims()
        {
            var raw = User.FindFirst("Region")?.Value;
            return int.TryParse(raw, out var code) ? code : null;
        }
    }
}
