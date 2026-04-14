using EcaInformationSystem.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace EcaInformationSystem.BlazorServer.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
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
    }
}