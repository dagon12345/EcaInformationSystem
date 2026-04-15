using EcaInformationSystem.Application.Interfaces.Services;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace EcaInformationSystem.Infrastructure.Services
{
    public class CurrentUserService : ICurrentUserService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        public CurrentUserService(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }
        public string GetUserName()
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext == null)
                return "Anonymous";
            var user = httpContext.User;
            if (user?.Identity == null || !user.Identity.IsAuthenticated)
                return "Anonymous";
            var username =
                user.FindFirst(ClaimTypes.Name)?.Value ??
                user.Identity.Name ??
                user.FindFirst("UserName")?.Value;
            return string.IsNullOrWhiteSpace(username) ? "Anonymous" : username;
        }
    }
}
