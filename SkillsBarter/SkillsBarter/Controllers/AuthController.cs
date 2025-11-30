using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.Facebook;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SkillsBarter.DTOs;
using SkillsBarter.Models;
using SkillsBarter.Services;

namespace SkillsBarter.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ITokenService _tokenService;
    private readonly IUserService _userService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        ITokenService tokenService,
        IUserService userService,
        ILogger<AuthController> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _tokenService = tokenService;
        _userService = userService;
        _logger = logger;
    }

    [HttpGet("login-google")]
    public IActionResult LoginGoogle()
    {
        var properties = new AuthenticationProperties 
        { 
            RedirectUri = Url.Action("GoogleCallback") 
        };
        return Challenge(properties, GoogleDefaults.AuthenticationScheme);
    }

    [HttpGet("login-facebook")]
    public IActionResult LoginFacebook()
    {
        var properties = new AuthenticationProperties 
        { 
            RedirectUri = Url.Action("FacebookCallback") 
        };
        return Challenge(properties, FacebookDefaults.AuthenticationScheme);
    }

    [HttpGet("google-callback")]
    [ApiExplorerSettings(IgnoreApi = true)]
    public async Task<IActionResult> GoogleCallback()
    {
        var result = await HttpContext.AuthenticateAsync(GoogleDefaults.AuthenticationScheme);
        if (!result.Succeeded) 
            return BadRequest(new { error = "Google authentication failed" });

        return await HandleExternalLogin(result);
    }

    [HttpGet("facebook-callback")]
    [ApiExplorerSettings(IgnoreApi = true)]
    public async Task<IActionResult> FacebookCallback()
    {
        var result = await HttpContext.AuthenticateAsync(FacebookDefaults.AuthenticationScheme);
        if (!result.Succeeded) 
            return BadRequest(new { error = "Facebook authentication failed" });

        return await HandleExternalLogin(result);
    }

    private async Task<IActionResult> HandleExternalLogin(Microsoft.AspNetCore.Authentication.AuthenticateResult authResult)
    {
        var claims = authResult.Principal?.Claims;
        var email = authResult.Principal?.FindFirst(ClaimTypes.Email)?.Value;
        var name = authResult.Principal?.FindFirst(ClaimTypes.Name)?.Value;

        if (string.IsNullOrEmpty(email))
            return BadRequest(new AuthResponse { Success = false, Message = "Email not provided by OAuth provider" });

        var user = await _userManager.FindByEmailAsync(email);

        if (user == null)
        {
            user = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = email,
                Email = email,
                Name = name ?? email.Split('@')[0],
                EmailConfirmed = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            var createResult = await _userManager.CreateAsync(user);
            if (!createResult.Succeeded)
            {
                var errors = createResult.Errors.Select(e => e.Description).ToList();
                return BadRequest(new AuthResponse { Success = false, Message = "Failed to create user", Errors = errors });
            }

            _logger.LogInformation($"New user created via OAuth: {user.Email}");
        }
        else
        {
            if (string.IsNullOrEmpty(user.Name) && !string.IsNullOrEmpty(name))
            {
                user.Name = name;
                user.UpdatedAt = DateTime.UtcNow;
                await _userManager.UpdateAsync(user);
            }
        }

        _logger.LogInformation($"User {user.Email} logged in via OAuth");

        var roles = await _userManager.GetRolesAsync(user);

        var token = _tokenService.GenerateAccessToken(user, roles);

        return Ok(new AuthResponse
        {
            Success = true,
            Message = "Successfully authenticated via OAuth",
            Token = token,
            User = await MapToUserDto(user)
        });
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList();
            return BadRequest(new AuthResponse
            {
                Success = false,
                Message = "Validation failed",
                Errors = errors
            });
        }

        // Normalizing email to lowercase for consistency,
        // since some users may use upper case
        var normalizedEmail = request.Email.ToLowerInvariant().Trim();

        // Checking if user already exists
        var existingUser = await _userManager.FindByEmailAsync(normalizedEmail);
        if (existingUser != null)
        {
            _logger.LogWarning($"Registration attempt with existing email: {normalizedEmail}");
            return BadRequest(new AuthResponse
            {
                Success = false,
                Message = "User with this email already exists"
            });
        }

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = normalizedEmail,
            Email = normalizedEmail,
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            // Until email service is set up, we mark email as confirmed
            EmailConfirmed = false, 
            UpdatedAt = DateTime.UtcNow
        };

        var result = await _userManager.CreateAsync(user, request.Password);

        if (!result.Succeeded)
        {
            var errors = result.Errors.Select(e => e.Description).ToList();
            _logger.LogError($"Failed to create user {normalizedEmail}: {string.Join(", ", errors)}");
            return BadRequest(new AuthResponse
            {
                Success = false,
                Message = "Failed to create user",
                Errors = errors
            });
        }

        try
        {
            // Assigning default Freemium role to new user
            var roleResult = await _userManager.AddToRoleAsync(user, "Freemium");
            if (!roleResult.Succeeded)
            {
                _logger.LogWarning($"Failed to assign Freemium role to user {user.Email}");
            }

            _logger.LogInformation($"User {user.Email} registered successfully with ID: {user.Id}");

            // Getting user roles and generating JWT token
            var roles = await _userManager.GetRolesAsync(user);
            var token = _tokenService.GenerateAccessToken(user, roles);

            return Ok(new AuthResponse
            {
                Success = true,
                Message = "User registered successfully",
                Token = token,
                User = await MapToUserDto(user)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error during post-registration setup for user {user.Email}");

            var roles = await _userManager.GetRolesAsync(user);
            var token = _tokenService.GenerateAccessToken(user, roles);

            return Ok(new AuthResponse
            {
                Success = true,
                Message = "User registered successfully",
                Token = token,
                User = await MapToUserDto(user)
            });
        }
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new AuthResponse { Success = false, Message = "Email and password are required" });

        // Finding user by email
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null)
            return Unauthorized(new AuthResponse { Success = false, Message = "Invalid email or password" });

        // Checking password
        var passwordValid = await _userManager.CheckPasswordAsync(user, request.Password);
        if (!passwordValid)
        {
            _logger.LogWarning($"Failed login attempt for user {request.Email}");
            return Unauthorized(new AuthResponse { Success = false, Message = "Invalid email or password" });
        }

        _logger.LogInformation($"User {user.Email} logged in successfully");

        var roles = await _userManager.GetRolesAsync(user);

        var token = _tokenService.GenerateAccessToken(user, roles);

        return Ok(new AuthResponse
        {
            Success = true,
            Message = "Login successful",
            Token = token,
            User = await MapToUserDto(user)
        });
    }

    [HttpPost("logout")]
    [Authorize]
    public IActionResult Logout()
    {
        // With JWT, logout is handled client-side by removing the token
        _logger.LogInformation($"User logged out");
        return Ok(new { success = true, message = "Successfully logged out. Please remove the token from client." });
    }

    [HttpGet("profile")]
    [Authorize]
    public async Task<IActionResult> GetCurrentUserProfile()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            _logger.LogWarning("Unauthorized access attempt to profile endpoint");
            return Unauthorized(new { success = false, message = "User not found" });
        }

        var detailedProfile = await _userService.GetDetailedProfileAsync(user.Id);
        if (detailedProfile == null)
        {
            _logger.LogError("Failed to retrieve detailed profile for user {UserId}", user.Id);
            return StatusCode(500, new { success = false, message = "Failed to retrieve user profile" });
        }

        return Ok(new
        {
            success = true,
            profile = detailedProfile
        });
    }

    private async Task<UserDto> MapToUserDto(ApplicationUser user)
    {
        var roles = await _userManager.GetRolesAsync(user);
        return new UserDto
        {
            Id = user.Id,
            Email = user.Email ?? string.Empty,
            UserName = user.UserName ?? string.Empty,
            Name = user.Name,
            Description = user.Description,
            IsModerator = user.IsModerator,
            CreatedAt = user.CreatedAt,
            EmailConfirmed = user.EmailConfirmed,
            Roles = roles.ToList()
        };
    }
}