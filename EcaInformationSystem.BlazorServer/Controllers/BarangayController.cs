using EcaInformationSystem.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace EcaInformationSystem.BlazorServer.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
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
    }
}
