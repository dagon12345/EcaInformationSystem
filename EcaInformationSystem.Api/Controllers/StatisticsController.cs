using EcaInformationSystem.Application.Interfaces.Services;
using EcaInformationSystem.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EcaInformationSystem.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class StatisticsController : ControllerBase
    {
        private readonly IStatisticsService _statisticsService;

        public StatisticsController(IStatisticsService statisticsService)
        {
            _statisticsService = statisticsService;
        }

        // PayrollQuarter is a bare 1-4 that repeats every fiscal year (no year
        // component of its own) — filtering by quarter without also pinning a
        // FiscalYear silently matches that quarter across EVERY year and sums
        // them together, which reads as "wrong"/inflated totals rather than
        // an intentional multi-year view. Reject it here instead.
        private static bool QuarterMissingYear(StatisticsRequestDto request) =>
            request.PayrollQuarter.HasValue && !request.FiscalYear.HasValue;

        [HttpPost("report")]
        public async Task<IActionResult> GetReport([FromBody] StatisticsRequestDto request)
        {
            if (QuarterMissingYear(request))
                return BadRequest(new { message = "Select a Fiscal Year along with Payroll Quarter — a quarter number alone repeats every year." });

            var report = await _statisticsService.GetStatisticsReportAsync(request);
            return Ok(report);
        }

        // ✅ NEW — "audit this count" modal: who exactly is included in a given
        // summary-card bucket, under the same filters as the report above.
        [HttpPost("members")]
        public async Task<IActionResult> GetMembers([FromBody] StatisticsMembersRequestDto request)
        {
            if (QuarterMissingYear(request.Filter))
                return BadRequest(new { message = "Select a Fiscal Year along with Payroll Quarter — a quarter number alone repeats every year." });

            var members = await _statisticsService.GetStatisticsMembersAsync(request);
            return Ok(members);
        }
    }
}