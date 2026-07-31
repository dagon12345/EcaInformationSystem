using EcaInformationSystem.Application.Interfaces.Services;
using EcaInformationSystem.Shared.DTOs.DailyAccomplishmentReport;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EcaInformationSystem.Api.Controllers
{
    // Strictly private — every route is scoped to the caller's own user id.
    // There is deliberately no admin or "view other employee's reports" route.
    [ApiController]
    [Route("api/darreport")]
    [Authorize(Policy = AuthPolicies.CookieOrJwt)]
    public class DarReportController : ControllerBase
    {
        private readonly IDarReportService _service;

        public DarReportController(IDarReportService service)
        {
            _service = service;
        }

        private Guid CurrentUserId => Guid.Parse(User.FindFirst("sub")!.Value);

        [HttpGet]
        public async Task<IActionResult> GetMine()
            => Ok(await _service.GetMineAsync(CurrentUserId));

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var result = await _service.GetByIdAsync(id, CurrentUserId);
            return result is null ? NotFound() : Ok(result);
        }

        [HttpPost]
        public async Task<IActionResult> Upsert([FromBody] DarReportUpsertDto dto)
        {
            try { return Ok(await _service.UpsertAsync(CurrentUserId, dto)); }
            catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
            catch (KeyNotFoundException) { return NotFound(); }
        }

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            await _service.DeleteAsync(CurrentUserId, id);
            return NoContent();
        }
    }
}
