using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.Facebook;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SkillsBarter.Models;

namespace SkillsBarter.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;

    public AuthController(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager)
    {
        _userManager = userManager;
        _signInManager = signInManager;
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
            return BadRequest(new { error = "Email not provided by OAuth provider" });

        // Find or create user
        var user = await _userManager.FindByEmailAsync(email);

        if (user == null)
        {
            user = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                CreatedAt = DateTime.UtcNow
            };

            var createResult = await _userManager.CreateAsync(user);
            if (!createResult.Succeeded)
                return BadRequest(new { errors = createResult.Errors });
        }

        await _signInManager.SignInAsync(user, isPersistent: false);

        // TODO: Generate JWT token here for API authentication
        return Ok(new
        {
            success = true,
            message = "Successfully authenticated",
            user = new
            {
                id = user.Id,
                email = user.Email,
                userName = user.UserName
            }
        });
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return Ok(new { message = "Successfully logged out" });
    }

    [HttpGet("me")]
    public async Task<IActionResult> GetCurrentUser()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
            return Unauthorized();

        return Ok(new
        {
            id = user.Id,
            email = user.Email,
            userName = user.UserName,
            createdAt = user.CreatedAt
        });
    }
}