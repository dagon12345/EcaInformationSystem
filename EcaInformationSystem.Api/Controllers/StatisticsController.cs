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

        [HttpPost("report")]
        public async Task<IActionResult> GetReport([FromBody] StatisticsRequestDto request)
        {
            var report = await _statisticsService.GetStatisticsReportAsync(request);
            return Ok(report);
        }

        // ✅ NEW — "audit this count" modal: who exactly is included in a given
        // summary-card bucket, under the same filters as the report above.
        [HttpPost("members")]
        public async Task<IActionResult> GetMembers([FromBody] StatisticsMembersRequestDto request)
        {
            var members = await _statisticsService.GetStatisticsMembersAsync(request);
            return Ok(members);
        }
    }
}