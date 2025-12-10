using System.Security.Claims;
using AspNetCoreRateLimit;

namespace SkillsBarter.Configuration;

public class ClientRateLimitResolver : IClientResolveContributor
{
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
