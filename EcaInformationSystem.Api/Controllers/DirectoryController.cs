using EcaInformationSystem.Api.Hubs;
using EcaInformationSystem.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EcaInformationSystem.Api.Controllers
{
    // Lists PDOs and Focals grouped by province, with live online status — this
    // is the "who do I call" surface for the voice call feature. Deliberately
    // reachable by Focal accounts too (they need to find people to call), unlike
    // almost every other controller in this API.
    [ApiController]
    [Route("api/directory")]
    [Authorize(Policy = "AnyAuthenticatedIncludingFocal")]
    public class DirectoryController : ControllerBase
    {
        private readonly IDirectoryService _service;
        private readonly VoiceCallTracker _voiceCallTracker;

        public DirectoryController(IDirectoryService service, VoiceCallTracker voiceCallTracker)
        {
            _service = service;
            _voiceCallTracker = voiceCallTracker;
        }

        [HttpGet("provincial")]
        public async Task<IActionResult> GetProvincial()
        {
            var groups = await _service.GetProvincialDirectoryAsync();

            foreach (var group in groups)
                foreach (var person in group.People)
                    person.IsOnline = _voiceCallTracker.IsOnline(person.UserId);

            return Ok(groups);
        }
    }
}
