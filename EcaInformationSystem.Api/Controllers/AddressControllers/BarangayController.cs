using EcaInformationSystem.Application.DTOs;
using EcaInformationSystem.Application.Interfaces;
using EcaInformationSystem.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EcaInformationSystem.Api.Controllers.AddressControllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class BarangayController : ControllerBase
    {
        private readonly IBarangayService _barangayService;
        public BarangayController(IBarangayService barangayService)
        {
            _barangayService = barangayService;
        }
        [HttpGet("by-municipality/{psgcCodeMunicipality}")]
        public async Task<IActionResult> GetByMunicipality(int psgcCodeMunicipality)
        {
            var barangays = await _barangayService.GetByMunicipalityCodeAsync(psgcCodeMunicipality);
            return Ok(barangays);
        }
        [HttpGet]
        public async Task<IActionResult> GetBarangays()
        {
            var barangays = await _barangayService.GetBarangaysAsync();
            return Ok(barangays);
        }
        [HttpPost("by-municipalities")]
        public async Task<IActionResult> GetByMunicipalities([FromBody] LookupMultiRequestDto request)
        {
            if (request == null || request.Ids == null || !request.Ids.Any())
                return Ok(new List<Barangay>());
            var result = await _barangayService.GetByMunicipalityIdsAsync(request.Ids);
            return Ok(result);
        }
    }
}
