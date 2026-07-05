using EcaInformationSystem.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EcaInformationSystem.Api.Controllers.AddressControllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class RegionController : ControllerBase
    {
        private readonly IRegionService _regionService;

        public RegionController(IRegionService regionService)
        {
            _regionService = regionService;
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetAll()
        {
            var regions = await _regionService.GetAllAsync();
            return Ok(regions.OrderBy(r => r.Name));
        }
    }
}
