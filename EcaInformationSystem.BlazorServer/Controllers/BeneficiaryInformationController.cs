using EcaInformationSystem.Application.DTOs;
using EcaInformationSystem.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace EcaInformationSystem.BlazorServer.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class BeneficiaryInformationController : ControllerBase
    {
        private readonly IBeneficiaryInformationService _beneficiaryInformationService;
        public BeneficiaryInformationController(IBeneficiaryInformationService beneficiaryInformationService)
        {
            _beneficiaryInformationService = beneficiaryInformationService;
        }
        private string GetCurrentUserName()
        {
            return User.FindFirst(ClaimTypes.Name)?.Value
                ?? User.FindFirst("UserName")?.Value
                ?? User.Identity?.Name
                ?? "Anonymous";
        }

        [HttpGet]
        public async Task<ActionResult<List<BeneficiaryInformationDto>>> Get()
        {
            var list = await _beneficiaryInformationService.GetAllAsync();
            return Ok(list);
        }
        [HttpPost]
        public async Task<ActionResult<BeneficiaryInformationDto>> Post([FromBody] CreateBeneficiaryInformationDto dto)
        {
            try
            {
                var userName = GetCurrentUserName();
                var result = await _beneficiaryInformationService.CreateAsync(dto, userName);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Put(Guid id, [FromBody] BeneficiaryInformationDto dto)
        {
            if (id != dto.Id)
                return BadRequest("ID mismatch.");
            try
            {
                var userName = GetCurrentUserName();
                await _beneficiaryInformationService.UpdateAsync(id, dto, userName);
                return NoContent();
            }
            catch (Exception ex)
            {
                return NotFound(ex.Message);
            }
        }
        [HttpPut("soft-delete/{id:guid}")]
        public async Task<IActionResult> SoftDeletePut(Guid id)
        {
            try
            {
                var userName = GetCurrentUserName();
                await _beneficiaryInformationService.SoftDeleteAsync(id, userName);
                return NoContent();
            }
            catch (Exception ex)
            {
                return NotFound(ex.Message);
            }
        }
        [HttpGet("filter")]
        public async Task<ActionResult<List<BeneficiaryInformationDto>>> Filter([FromQuery] BeneficiaryFilterDto filter)
        {
            var result = await _beneficiaryInformationService.FilterAsync(filter);
            return Ok(result);
        }
        [HttpGet("summary")]
        public async Task<ActionResult<BeneficiarySummaryResultDto>> GetSummary([FromQuery] BeneficiaryFilterDto filter)
        {
            var result = await _beneficiaryInformationService.GetSummaryAsync(filter);
            return Ok(result);
        }
        [HttpPost("import")]
        [Consumes("multipart/form-data")]
        [ProducesResponseType(typeof(BeneficiaryImportResultDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> ImportExcel([FromForm] ImportBeneficiaryExcelRequestDto request)
        {
            try
            {
                if (request.File == null || request.File.Length == 0)
                    return BadRequest("Please upload a valid Excel file.");

                var userName = GetCurrentUserName();

                using var stream = request.File.OpenReadStream();

                var result = await _beneficiaryInformationService.ImportExcelAsync(
                    stream,
                    request.File.FileName,
                    userName);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
        [HttpGet("whoami")]
        public IActionResult WhoAmI()
        {
            return Ok(new
            {
                IsAuthenticated = User.Identity?.IsAuthenticated,
                Name = User.Identity?.Name,
                Claims = User.Claims.Select(c => new { c.Type, c.Value }).ToList()
            });
        }

    }
}
