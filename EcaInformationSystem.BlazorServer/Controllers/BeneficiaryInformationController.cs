using EcaInformationSystem.Application.DTOs;
using EcaInformationSystem.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace EcaInformationSystem.BlazorServer.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class BeneficiaryInformationController : ControllerBase
    {
        private readonly IBeneficiaryInformationService _beneficiaryInformationService;
        public BeneficiaryInformationController(IBeneficiaryInformationService beneficiaryInformationService)
        {
            _beneficiaryInformationService = beneficiaryInformationService;
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
                var result = await _beneficiaryInformationService.CreateAsync(dto);
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
                await _beneficiaryInformationService.UpdateAsync(id, dto);
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
                await _beneficiaryInformationService.SoftDeleteAsync(id);
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
        public async Task<IActionResult> ImportExcel([FromForm] IFormFile file)
        {
            try
            {
                var result = await _beneficiaryInformationService.ImportExcelAsync(file);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
    }
}
