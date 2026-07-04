using EcaInformationSystem.Application.Interfaces;
using EcaInformationSystem.Application.Interfaces.Services;
using EcaInformationSystem.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EcaInformationSystem.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Policy = "AdminOnly")]
    public class LogsController : ControllerBase
    {
        private readonly IBeneficiaryInformationService _service;

        public LogsController(IBeneficiaryInformationService service)
        {
            _service = service;
        }

        [HttpPost("all")]
        public async Task<IActionResult> GetAllLogs([FromBody] LogFilterDto filter)
        {
            var result = await _service.GetAllLogsAsync(filter);
            return Ok(result);
        }
    }
}