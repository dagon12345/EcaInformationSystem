using EcaInformationSystem.Application.Common.Models;
using EcaInformationSystem.Application.Interfaces.Repositories;
using EcaInformationSystem.Shared.DTOs.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using System.IdentityModel.Tokens.Jwt;

namespace EcaInformationSystem.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    // ✅ Every endpoint here is scoped to CurrentUserId — Focal accounts can
    // manage their own sessions from their profile page too, same reasoning
    // as UserProfileController's "me" endpoints.
    [Authorize(Policy = "AnyAuthenticatedIncludingFocal")]
    public class UserSessionsController : ControllerBase
    {
        private readonly IUserSessionRepository _userSessionRepository;
        private readonly IMemoryCache _cache;

        public UserSessionsController(IUserSessionRepository userSessionRepository, IMemoryCache cache)
        {
            _userSessionRepository = userSessionRepository;
            _cache = cache;
        }

        private Guid CurrentUserId => Guid.Parse(User.FindFirst("sub")!.Value);
        private string? CurrentJti => User.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;

        [HttpGet]
        public async Task<IActionResult> GetSessions()
        {
            var sessions = await _userSessionRepository.GetActiveSessionsForUserAsync(CurrentUserId);

            var dtos = sessions.Select(s => new SessionDto
            {
                Id = s.Id,
                DeviceLabel = s.DeviceLabel,
                IpAddress = s.IpAddress,
                LoginAt = s.LoginAt,
                LastActiveAt = s.LastActiveAt,
                ExpiresAt = s.ExpiresAt,
                IsCurrent = s.Jti == CurrentJti
            });

            return Ok(dtos);
        }

        [HttpPost("{id:guid}/revoke")]
        public async Task<IActionResult> RevokeSession(Guid id)
        {
            var session = await _userSessionRepository.GetByIdForUserAsync(id, CurrentUserId);
            if (session == null) return NotFound(new { message = "Session not found." });

            if (session.Jti == CurrentJti)
                return BadRequest(new { message = "You can't remotely log out the device you're currently using." });

            session.RevokedAt = DateTime.UtcNow;
            await _userSessionRepository.SaveChangesAsync();

            // ✅ evict the cached "not revoked" verdict so this jti is rejected on its very next request
            _cache.Remove($"revoked-session:{session.Jti}");

            return Ok(new { message = "Session revoked." });
        }

        [HttpPost("revoke-others")]
        public async Task<IActionResult> RevokeOtherSessions()
        {
            var sessions = await _userSessionRepository.GetActiveSessionsForUserAsync(CurrentUserId);
            var currentJti = CurrentJti;

            foreach (var session in sessions.Where(s => s.Jti != currentJti))
            {
                session.RevokedAt = DateTime.UtcNow;
                _cache.Remove($"revoked-session:{session.Jti}");
            }

            await _userSessionRepository.SaveChangesAsync();

            return Ok(new { message = "All other sessions have been logged out." });
        }
    }
}
