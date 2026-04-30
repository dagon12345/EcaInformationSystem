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
    public class MunicipalityController : ControllerBase
    {
        private readonly IMunicipalityService _municipalityService;

        public MunicipalityController(IMunicipalityService municipalityService)
        {
            _municipalityService = municipalityService;
        }
        [HttpGet("by-province/{psgcCodeProvince}")]
        public async Task<IActionResult> GetByProvince(int psgcCodeProvince)
        {
            var municipalities = await _municipalityService.GetByProvinceCodeAsync(psgcCodeProvince);
            return Ok(municipalities);
        }
        [HttpGet]
        public async Task<IActionResult> GetAllMunicipalities()
        {
            var municipalities = await _municipalityService.GetMunicipalitiesAsync();
            return Ok(municipalities);
        }
        [HttpPost("by-provinces")]
        public async Task<IActionResult> GetByProvinces([FromBody] LookupMultiRequestDto request)
        {
            if (request == null || request.Ids == null || !request.Ids.Any())
                return Ok(new List<Municipality>());
            var result = await _municipalityService.GetByProvinceIdsAsync(request.Ids);
            return Ok(result);
        }
    }
}
