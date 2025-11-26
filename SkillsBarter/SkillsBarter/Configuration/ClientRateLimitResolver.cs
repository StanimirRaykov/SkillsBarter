using AspNetCoreRateLimit;
using Microsoft.AspNetCore.Identity;
using SkillsBarter.Models;
using System.Security.Claims;

namespace SkillsBarter.Configuration;

public class ClientRateLimitResolver : IClientResolveContributor
{
    private readonly UserManager<ApplicationUser> _userManager;

    public ClientRateLimitResolver(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    public Task<string> ResolveClientAsync(HttpContext httpContext)
    {
        var clientId = "anonymous";

        if (httpContext.User?.Identity?.IsAuthenticated == true)
        {
            var userId = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrEmpty(userId))
            {
                clientId = $"user_{userId}";
            }
        }
        else
        {
            var ip = httpContext.Connection.RemoteIpAddress?.ToString();
            if (!string.IsNullOrEmpty(ip))
            {
                clientId = $"ip_{ip}";
            }
        }

        return Task.FromResult(clientId);
    }
}
