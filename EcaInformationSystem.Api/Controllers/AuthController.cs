using EcaInformationSystem.Application.DTOs.Auth;
using EcaInformationSystem.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace EcaInformationSystem.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [AllowAnonymous]
    public class AuthController: ControllerBase
    {
        private readonly IAuthService _authService;
        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }
        [HttpPost("login")]
        [EnableRateLimiting("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            var result = await _authService.LoginAsync(request);

            if (!result.Success)
            {
                int? retryAfterSeconds = null;
                if (result.LockoutEndsAt.HasValue)
                {
                    var remaining = result.LockoutEndsAt.Value - DateTime.UtcNow;
                    retryAfterSeconds = (int)Math.Ceiling(Math.Max(remaining.TotalSeconds, 0));
                }

                return Unauthorized(new
                {
                    message = result.Message,
                    attemptsRemaining = result.AttemptsRemaining,
                    isLockedOut = result.IsLockedOut,
                    retryAfterSeconds // ✅ NEW — same field name as the 429 response
                });
            }

            return Ok(new
            {
                result.Token,
                result.FullName,
                result.UserName
            });

        }
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            var result = await _authService.RegisterAsync(request);
            if (!result.Success) return BadRequest(result.Message);
            return Ok(result);
        }
    }
}
