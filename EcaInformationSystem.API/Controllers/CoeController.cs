// Api/Controllers/CoeController.cs
using EcaInformationSystem.Shared.DTOs;
using EcaInformationSystem.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EcaInformationSystem.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class CoeController : ControllerBase
    {
        private readonly ICoeService _service;
        public CoeController(ICoeService service) => _service = service;

        [HttpPost("preview")]
        public async Task<IActionResult> Preview([FromBody] CoeSettingsDto settings)
        {
            try { return Ok(await _service.BuildCoePreviewAsync(settings)); }
            catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
        }

        [HttpPost("generate")]
        [Authorize(Policy = "AdminOrPDO")]
        public async Task<IActionResult> Generate([FromBody] CoeSettingsDto settings)
        {
            try
            {
                var userName = User.Identity?.Name ?? "System";
                var bytes = await _service.GenerateCoeAsync(settings, userName);
                return File(bytes,
                    "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                    $"COE_{DateTime.Today:yyyy-MM-dd}.docx");
            }
            catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
        }
    }
}