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
            var created = await _beneficiaryInformationService.CreateAsync(dto);
            return Ok(created);
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
    }
}
