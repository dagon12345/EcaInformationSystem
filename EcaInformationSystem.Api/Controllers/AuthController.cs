using EcaInformationSystem.Application.DTOs.Auth;
using EcaInformationSystem.Application.Interfaces.Services;
using EcaInformationSystem.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace EcaInformationSystem.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController: ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly IPasswordResetService _passwordResetService; // ✅ NEW — add to constructor
        public AuthController(IAuthService authService, IPasswordResetService passwordResetService)
        {
            _authService = authService;
            _passwordResetService = passwordResetService;
        }

        // ✅ NEW — same IP-resolution approach as the login rate limiter (Program.cs)
        private string GetClientIp() =>
            HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        private string GetUserAgent() =>
            Request.Headers.UserAgent.ToString() is { Length: > 0 } ua ? ua : "unknown";

        [HttpPost("login")]
        [AllowAnonymous]
        [EnableRateLimiting("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            var result = await _authService.LoginAsync(request, GetClientIp(), GetUserAgent());

            if (result.MfaRequired) // ✅ NEW — distinct from Success/Failed
            {
                return Ok(new { mfaRequired = true, userId = result.UserId });
            }
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
                    retryAfterSeconds
                });
            }

            return Ok(new
            {
                result.Token,
                result.FullName,
                result.UserName,
                result.ShowMfaPrompt // ✅ NEW
            });

        }
        [HttpPost("mfa/dismiss-prompt")]
        [Authorize(Policy = AuthPolicies.CookieOrJwt)]
        public async Task<IActionResult> DismissMfaPrompt()
        {
            var userId = Guid.Parse(User.FindFirst("sub")!.Value);
            await _authService.MarkMfaPromptShownAsync(userId);
            return Ok();
        }
        // ✅ NEW — second step, only reached if Login returned mfaRequired: true
        [HttpPost("verify-mfa")]
        [AllowAnonymous]
        [EnableRateLimiting("login")] // same throttle — 6-digit codes are guessable without it
        public async Task<IActionResult> VerifyMfa([FromBody] VerifyMfaRequest request)
        {
            var result = await _authService.VerifyMfaAndIssueTokenAsync(request.UserId, request.Code, GetClientIp(), GetUserAgent());
            if (!result.Success) return Unauthorized(new { message = result.Message });

            return Ok(new { result.Token, result.FullName, result.UserName });
        }
        [HttpPost("mfa/setup")]
        [Authorize(Policy = AuthPolicies.CookieOrJwt)]
        public async Task<IActionResult> SetupMfa()
        {
            var userId = Guid.Parse(User.FindFirst("sub")!.Value);
            try
            {
                var result = await _authService.GenerateMfaSecretAsync(userId);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("mfa/confirm")]
        [Authorize(Policy = AuthPolicies.CookieOrJwt)]
        public async Task<IActionResult> ConfirmMfa([FromBody] ConfirmMfaRequest request)
        {
            var userId = Guid.Parse(User.FindFirst("sub")!.Value);
            var success = await _authService.ConfirmMfaSetupAsync(userId, request.Code);
            if (!success) return BadRequest(new { message = "Invalid code. Please try again." });
            return Ok(new { message = "MFA enabled successfully." });
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            var result = await _authService.RegisterAsync(request);
            if (!result.Success) return BadRequest(result.Message);
            return Ok(result);
        }
        [HttpPost("mfa/reset")]
        [Authorize(Policy = AuthPolicies.CookieOrJwt)] // requires being logged in — see note below
        public async Task<IActionResult> ResetMfa([FromBody] ResetMfaRequest request)
        {
            var userId = Guid.Parse(User.FindFirst("sub")!.Value);
            var success = await _authService.ResetMfaAsync(userId, request.CurrentPassword);
            if (!success) return BadRequest(new { message = "Incorrect password." });
            return Ok(new { message = "MFA has been reset. Please set it up again." });
        }
        [HttpPost("mfa/admin-reset/{targetUserId:guid}")]
        [Authorize(Policy = "SuperAdminOnly")] // reuses your existing policy — Admin or SuperAdmin
        public async Task<IActionResult> AdminResetMfa(Guid targetUserId)
        {
            try
            {
                await _authService.ResetMfaByAdminAsync(targetUserId);
                return Ok(new { message = "MFA has been reset for this user." });
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new { message = "User not found." });
            }
        }
        [HttpGet("mfa/status")]
        [Authorize(Policy = AuthPolicies.CookieOrJwt)]
        public async Task<IActionResult> GetMfaStatus()
        {
            var userId = Guid.Parse(User.FindFirst("sub")!.Value);
            var enabled = await _authService.IsMfaEnabledAsync(userId);
            return Ok(new { isMfaEnabled = enabled });
        }

        [HttpPost("password-reset/request")]
        [AllowAnonymous]
        [EnableRateLimiting("login")] // ✅ same throttle — prevents spamming reset requests
        public async Task<IActionResult> RequestPasswordReset([FromBody] Shared.DTOs.Auth.RequestPasswordResetDto dto)
        {
            await _passwordResetService.RequestResetAsync(dto.UserName);
            // ✅ Always the same response, whether the username exists or not
            return Ok(new { message = "If this account exists, your request has been sent to an administrator. Please wait for confirmation." });
        }

        [HttpPost("password-reset/complete")]
        [AllowAnonymous]
        [EnableRateLimiting("login")] // ✅ code-guessing surface, same protection as MFA verify
        public async Task<IActionResult> CompletePasswordReset([FromBody] Shared.DTOs.Auth.ResetPasswordDto dto)
        {
            var success = await _passwordResetService.ResetPasswordAsync(dto.UserName, dto.Code, dto.NewPassword);
            if (!success) return BadRequest(new { message = "Invalid or expired code." });
            return Ok(new { message = "Password reset successfully. You can now log in." });
        }
    }
}
