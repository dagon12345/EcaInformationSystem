using EcaInformationSystem.Application.Common.Models;
using EcaInformationSystem.Application.Interfaces.Services;
using EcaInformationSystem.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EcaInformationSystem.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UserProfileController : ControllerBase
    {
        private readonly IUserProfileService _profileService;
        private readonly IAuthService _authService;

        public UserProfileController(IUserProfileService profileService, IAuthService authService)
        {
            _profileService = profileService;
            _authService = authService;
        }

        [HttpGet("me")]
        [Authorize(Policy = AuthPolicies.CookieOrJwt)]
        public async Task<IActionResult> GetMyProfile()
        {
            var userId = Guid.Parse(User.FindFirst("sub")!.Value);
            var profile = await _profileService.GetMyProfileAsync(userId);
            if (profile is null) return NotFound();
            return Ok(profile);
        }

        [HttpPut("me")]
        [Authorize(Policy = AuthPolicies.CookieOrJwt)]
        public async Task<IActionResult> UpdateMyProfile([FromBody] UpdateProfileRequestDto request)
        {
            if (string.IsNullOrWhiteSpace(request.FullName) || string.IsNullOrWhiteSpace(request.Position))
                return BadRequest(new { message = "Name and position are required." });

            var userId = Guid.Parse(User.FindFirst("sub")!.Value);
            var profile = await _profileService.UpdateProfileAsync(userId, request);

            // FullName/Position are embedded in the JWT — reissue so the client's
            // stored token (and everything decoded from it) reflects the edit
            // immediately, without asking the user to log out and back in.
            var reissued = await _authService.ReissueTokenAsync(userId);

            return Ok(new { profile, token = reissued.Token });
        }

        [HttpPost("me/picture")]
        [Authorize(Policy = AuthPolicies.CookieOrJwt)]
        [RequestSizeLimit(10_485_760)] // 10MB — resized/compressed server-side anyway
        public async Task<IActionResult> UploadMyPicture(IFormFile file)
        {
            if (file.Length == 0) return BadRequest(new { message = "No file uploaded." });
            if (!file.ContentType.StartsWith("image/"))
                return BadRequest(new { message = "Only image files are allowed." });

            var userId = Guid.Parse(User.FindFirst("sub")!.Value);
            var userName = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? "Unknown";

            using var stream = file.OpenReadStream();
            await _profileService.UploadProfilePictureAsync(userId, stream, userName);

            return Ok(new { message = "Profile picture updated." });
        }

        [HttpDelete("me/picture")]
        [Authorize(Policy = AuthPolicies.CookieOrJwt)]
        public async Task<IActionResult> RemoveMyPicture()
        {
            var userId = Guid.Parse(User.FindFirst("sub")!.Value);
            await _profileService.RemoveProfilePictureAsync(userId);
            return Ok(new { message = "Profile picture removed." });
        }

        // Internal-only (not AllowAnonymous) — surfaces upcoming staff birthdays
        // for the Feed's "welcome" widget.
        [HttpGet("upcoming-birthdays")]
        [Authorize(Policy = AuthPolicies.CookieOrJwt)]
        public async Task<IActionResult> GetUpcomingBirthdays([FromQuery] int withinDays = 7)
            => Ok(await _profileService.GetUpcomingBirthdaysAsync(withinDays));

        // Any logged-in user can view another user's basic public profile —
        // e.g. clicking an avatar/name in Chat or the Feed.
        [HttpGet("{userId:guid}")]
        [Authorize(Policy = AuthPolicies.CookieOrJwt)]
        public async Task<IActionResult> GetPublicProfile(Guid userId)
        {
            var profile = await _profileService.GetPublicProfileAsync(userId);
            if (profile is null) return NotFound();
            return Ok(profile);
        }

        // AllowAnonymous is deliberate here, same as PostsController's image
        // endpoints: <img src="..."> tags can't attach a Bearer token, so an
        // [Authorize]-gated image endpoint would 401 every avatar on the page.
        // The GUID being unguessable is the same protection already accepted
        // for post images elsewhere in this app.
        [HttpGet("{userId:guid}/avatar")]
        [AllowAnonymous]
        public async Task<IActionResult> GetAvatar(Guid userId)
        {
            var result = await _profileService.GetProfilePictureAsync(userId, thumbnail: false);
            if (result is null) return NotFound();

            Response.Headers["Cross-Origin-Resource-Policy"] = "cross-origin";
            // Same URL is reused after every re-upload (it's keyed by userId, not
            // by picture version), so without this the browser can keep serving a
            // cached copy of someone's OLD picture in Feed/Chat/NavMenu after they
            // change it — no-store forces a fresh fetch every time it's rendered.
            Response.Headers["Cache-Control"] = "no-store";
            return File(result.Value.Data, result.Value.ContentType);
        }

        [HttpGet("{userId:guid}/avatar/thumb")]
        [AllowAnonymous]
        public async Task<IActionResult> GetAvatarThumbnail(Guid userId)
        {
            var result = await _profileService.GetProfilePictureAsync(userId, thumbnail: true);
            if (result is null) return NotFound();

            Response.Headers["Cross-Origin-Resource-Policy"] = "cross-origin";
            Response.Headers["Cache-Control"] = "no-store";
            return File(result.Value.Data, result.Value.ContentType);
        }
    }
}
