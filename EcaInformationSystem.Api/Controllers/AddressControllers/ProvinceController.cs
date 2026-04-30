using EcaInformationSystem.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EcaInformationSystem.Api.Controllers.AddressControllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ProvinceController : ControllerBase
    {
        private readonly IProvinceService _provinceService;
        public ProvinceController(IProvinceService provinceService)
        {
            _provinceService = provinceService;
        }
        [HttpGet("by-region/{psgcCodeRegion}")]
        public async Task<IActionResult> GetByRegion(int psgcCodeRegion)
        {
            var provinces = await _provinceService.GetByRegionCodeAsync(psgcCodeRegion);
            return Ok(provinces);
        }
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var provinces = await _provinceService.GetAllProvinceAsync();
            return Ok(provinces);
        }
    }
}
