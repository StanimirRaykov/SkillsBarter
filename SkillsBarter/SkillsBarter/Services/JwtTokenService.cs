using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SkillsBarter.Models;

namespace SkillsBarter.Services;

public interface ITokenService
{
    string GenerateAccessToken(ApplicationUser user, IList<string>? roles = null);
    string GenerateRefreshToken(ApplicationUser user);
    Task<(bool IsValid, ApplicationUser? User)> ValidateRefreshTokenAsync(string refreshToken);
}

public class JwtTokenService : ITokenService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<JwtTokenService> _logger;
    private readonly UserManager<ApplicationUser> _userManager;

    public JwtTokenService(
        IConfiguration configuration,
        ILogger<JwtTokenService> logger,
        UserManager<ApplicationUser> userManager
    )
    {
        _configuration = configuration;
        _logger = logger;
        _userManager = userManager;
    }

    public string GenerateAccessToken(ApplicationUser user, IList<string>? roles = null)
    {
        var jwtSettings = _configuration.GetSection("Jwt");
        var secretKey = jwtSettings["SecretKey"];
        var issuer = jwtSettings["Issuer"];
        var audience = jwtSettings["Audience"];
        var expirationMinutes = int.Parse(jwtSettings["ExpirationMinutes"] ?? "60");

        if (string.IsNullOrEmpty(secretKey))
        {
            _logger.LogError("JWT SecretKey is not configured");
            throw new InvalidOperationException("JWT SecretKey is not configured");
        }

        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email ?? string.Empty),
            new Claim(ClaimTypes.Name, user.Name),
            new Claim("IsModerator", user.IsModerator.ToString())
        };

        if (roles != null && roles.Any())
        {
            foreach (var role in roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }
        }

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expirationMinutes),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerateRefreshToken(ApplicationUser user)
    {
        var jwtSettings = _configuration.GetSection("Jwt");
        var secretKey = jwtSettings["SecretKey"];
        var issuer = jwtSettings["Issuer"];
        var audience = jwtSettings["Audience"];
        var refreshTokenExpirationDays = int.Parse(
            jwtSettings["RefreshTokenExpirationDays"] ?? "7"
        );

        if (string.IsNullOrEmpty(secretKey))
        {
            _logger.LogError("JWT SecretKey is not configured");
            throw new InvalidOperationException("JWT SecretKey is not configured");
        }

        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var jti = Guid.NewGuid().ToString();

        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new Claim(JwtRegisteredClaimNames.Jti, jti),
            new Claim("TokenType", "RefreshToken")
        };

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddDays(refreshTokenExpirationDays),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public async Task<(bool IsValid, ApplicationUser? User)> ValidateRefreshTokenAsync(
        string refreshToken
    )
    {
        try
        {
            var jwtSettings = _configuration.GetSection("Jwt");
            var secretKey = jwtSettings["SecretKey"];
            var issuer = jwtSettings["Issuer"];
            var audience = jwtSettings["Audience"];

            if (string.IsNullOrEmpty(secretKey))
            {
                _logger.LogError("JWT SecretKey is not configured");
                return (false, null);
            }

            var tokenHandler = new JwtSecurityTokenHandler();
            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
                ValidateIssuer = true,
                ValidIssuer = issuer,
                ValidateAudience = true,
                ValidAudience = audience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            };

            var principal = tokenHandler.ValidateToken(
                refreshToken,
                validationParameters,
                out SecurityToken validatedToken
            );

            var jwtToken = validatedToken as JwtSecurityToken;

            if (
                jwtToken == null
                || !jwtToken.Header.Alg.Equals(
                    SecurityAlgorithms.HmacSha256,
                    StringComparison.InvariantCultureIgnoreCase
                )
            )
            {
                _logger.LogWarning("Invalid refresh token: algorithm mismatch");
                return (false, null);
            }

            var tokenTypeClaim = principal.FindFirst("TokenType")?.Value;
            if (tokenTypeClaim != "RefreshToken")
            {
                _logger.LogWarning("Invalid token type: expected RefreshToken");
                return (false, null);
            }

            var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;

            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            {
                _logger.LogWarning("Invalid refresh token: missing or invalid user ID");
                return (false, null);
            }

            var user = await _userManager
                .Users.FirstOrDefaultAsync(u =>
                    u.Id == userId && u.RefreshToken == refreshToken
                );

            if (user == null)
            {
                _logger.LogWarning("Invalid refresh token: user not found or token mismatch");
                return (false, null);
            }

            if (user.RefreshTokenExpiry == null || user.RefreshTokenExpiry < DateTime.UtcNow)
            {
                _logger.LogWarning($"Refresh token expired in database for user {user.Id}");
                return (false, null);
            }

            return (true, user);
        }
        catch (SecurityTokenExpiredException)
        {
            _logger.LogWarning("Refresh token has expired");
            return (false, null);
        }
        catch (SecurityTokenException ex)
        {
            _logger.LogWarning($"Invalid refresh token: {ex.Message}");
            return (false, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating refresh token");
            return (false, null);
        }
    }
}
