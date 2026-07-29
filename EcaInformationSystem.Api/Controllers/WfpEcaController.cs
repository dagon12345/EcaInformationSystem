using EcaInformationSystem.Application.Interfaces.Services;
using EcaInformationSystem.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EcaInformationSystem.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class WfpEcaController : ControllerBase
    {
        private readonly IWfpEcaService _service;
        private readonly ICurrentUserService _currentUser;

        public WfpEcaController(IWfpEcaService service, ICurrentUserService currentUser)
        {
            _service = service;
            _currentUser = currentUser;
        }

        // Any authenticated user can view their own region's Work Financial
        // Plan — only editing it is Admin/SuperAdmin-only, enforced in Upsert.
        [HttpGet("{fiscalYear:int}")]
        public async Task<IActionResult> Get(int fiscalYear)
        {
            var regionCode = GetRegionCodeFromClaims();
            if (regionCode is null)
                return BadRequest("No region is assigned to this account.");

            var result = await _service.GetAsync(regionCode.Value, fiscalYear);
            return Ok(result);
        }

        [HttpPut]
        public async Task<IActionResult> Upsert([FromBody] UpsertWfpEcaDto dto)
        {
            if (!User.IsInRole("Admin") && !User.IsInRole("SuperAdmin"))
                return Forbid();

            var regionCode = GetRegionCodeFromClaims();
            if (regionCode is null)
                return BadRequest("No region is assigned to this account.");

            if (dto.Lines is null)
                return BadRequest("Lines is required.");

            if (dto.Lines.Any(l => l.Allotment < 0 || l.Obligation < 0))
                return BadRequest("Allotment and Obligation cannot be negative.");

            if (dto.Lines.Any(l => string.IsNullOrWhiteSpace(l.UacsCode) || string.IsNullOrWhiteSpace(l.UacsName)))
                return BadRequest("Every line item needs a UACS Code and UACS Name.");

            if (dto.Lines.Select(l => l.UacsCode.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() != dto.Lines.Count)
                return BadRequest("UACS Codes must be unique — two rows have the same code.");

            var userName = _currentUser.GetUserName();

            try
            {
                var result = await _service.UpsertAsync(regionCode.Value, dto.FiscalYear, dto.Lines, userName);
                return Ok(result);
            }
            catch (DbUpdateException)
            {
                return BadRequest("Could not save — check that no two rows share the same UACS Code.");
            }
        }

        private int? GetRegionCodeFromClaims()
        {
            var raw = User.FindFirst("Region")?.Value;
            return int.TryParse(raw, out var code) ? code : null;
        }
    }
}
