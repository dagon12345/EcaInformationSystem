using EcaInformationSystem.Application.Interfaces.Services;
using EcaInformationSystem.Domain.Common.Enum;
using EcaInformationSystem.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace EcaInformationSystem.Api.Controllers
{
    [ApiController]
    [Route("api/beneficiary-finding")]
    [Authorize]
    public class BeneficiaryFindingController : ControllerBase
    {
        private readonly IBeneficiaryFindingService _service;

        public BeneficiaryFindingController(IBeneficiaryFindingService service)
        {
            _service = service;
        }

        // GET api/beneficiary-finding/{beneficiaryId}
        [HttpGet("{beneficiaryId:guid}")]
        public async Task<IActionResult> GetByBeneficiaryId(Guid beneficiaryId)
        {
            var result = await _service.GetByBeneficiaryIdAsync(beneficiaryId);

            if (result is null)
                return NotFound(new { Message = "No finding record found for this beneficiary." });

            return Ok(result);
        }

        // POST api/beneficiary-finding/{beneficiaryId}
        [HttpPost("{beneficiaryId:guid}")]
        public async Task<IActionResult> Upsert(Guid beneficiaryId, [FromBody] UpsertBeneficiaryFindingDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var userName = User.FindFirstValue(ClaimTypes.Name)
                        ?? User.FindFirstValue("name")
                        ?? CommonConstants.Unknown.ToTitleCase();

            var result = await _service.UpsertAsync(beneficiaryId, dto, userName);
            return Ok(result);
        }
    }
}
