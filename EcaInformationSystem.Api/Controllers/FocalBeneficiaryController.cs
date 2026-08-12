using EcaInformationSystem.Application.Interfaces.Services;
using EcaInformationSystem.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EcaInformationSystem.Api.Controllers
{
    // Read-only, single-municipality grantee view for external partner-LGU
    // "Focal" accounts. Deliberately its own controller, separate from
    // BeneficiaryController's full CRUD surface — the municipality scope is
    // always resolved server-side from the caller's own PdoJurisdiction rows
    // (see FocalBeneficiaryService), never accepted from the request.
    [ApiController]
    [Route("api/focal/beneficiaries")]
    [Authorize(Policy = "FocalOnly")]
    public class FocalBeneficiaryController : ControllerBase
    {
        private readonly IFocalBeneficiaryService _service;

        public FocalBeneficiaryController(IFocalBeneficiaryService service)
        {
            _service = service;
        }

        private Guid CurrentUserId => Guid.Parse(User.FindFirst("sub")!.Value);

        [HttpPost]
        public async Task<IActionResult> GetPaged([FromBody] FocalBeneficiaryFilterDto filter)
            => Ok(await _service.GetPagedAsync(CurrentUserId, filter));

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetDetail(Guid id)
        {
            try { return Ok(await _service.GetDetailAsync(CurrentUserId, id)); }
            catch (KeyNotFoundException) { return NotFound(); }
        }
    }
}
