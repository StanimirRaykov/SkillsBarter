using Microsoft.AspNetCore.Identity;
using SkillsBarter.Constants;
using SkillsBarter.Models;

namespace SkillsBarter.Services;

public class RoleSeeder
{
    private readonly RoleManager<IdentityRole<Guid>> _roleManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IConfiguration _configuration;
    private readonly ILogger<RoleSeeder> _logger;

    public RoleSeeder(
        RoleManager<IdentityRole<Guid>> roleManager,
        UserManager<ApplicationUser> userManager,
        IConfiguration configuration,
        ILogger<RoleSeeder> logger)
    {
        _roleManager = roleManager;
        _userManager = userManager;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SeedRolesAsync()
    {
        // Creates all roles if they don't exist
        var roles = new[] { AppRoles.Freemium, AppRoles.Premium, AppRoles.Moderator, AppRoles.Admin };

        foreach (var roleName in roles)
        {
            if (!await _roleManager.RoleExistsAsync(roleName))
            {
                var role = new IdentityRole<Guid>
                {
                    Id = Guid.NewGuid(),
                    Name = roleName,
                    NormalizedName = roleName.ToUpper()
                };

                var result = await _roleManager.CreateAsync(role);

                if (result.Succeeded)
                {
                    _logger.LogInformation("Role {RoleName} created successfully", roleName);
                }
                else
                {
                    _logger.LogError("Failed to create role {RoleName}: {Errors}",
                        roleName, string.Join(", ", result.Errors.Select(e => e.Description)));
                }
            }
        }
    }

    public async Task<bool> AssignRoleToUserAsync(Guid userId, string roleName)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            _logger.LogWarning("User {UserId} not found", userId);
            return false;
        }

        if (!await _roleManager.RoleExistsAsync(roleName))
        {
            _logger.LogWarning("Role {RoleName} does not exist", roleName);
            return false;
        }

        var currentRoles = await _userManager.GetRolesAsync(user);
        if (currentRoles.Any())
        {
            await _userManager.RemoveFromRolesAsync(user, currentRoles);
        }
        var result = await _userManager.AddToRoleAsync(user, roleName);

        if (result.Succeeded)
        {
            _logger.LogInformation("User {UserId} assigned to role {RoleName}", userId, roleName);
            return true;
        }

        _logger.LogError("Failed to assign user {UserId} to role {RoleName}: {Errors}",
            userId, roleName, string.Join(", ", result.Errors.Select(e => e.Description)));
        return false;
    }

    public async Task SeedAdminAndModeratorAsync()
    {
        await SeedRolesAsync();

        var adminEmail = _configuration["SeedUsers:Admin:Email"] ?? "admin@skillsbarter.com";
        var adminPassword = _configuration["SeedUsers:Admin:Password"] ?? "Admin@123456";
        var adminName = _configuration["SeedUsers:Admin:Name"] ?? "admin123";

        var moderatorEmail = _configuration["SeedUsers:Moderator:Email"] ?? "moderator@skillsbarter.com";
        var moderatorPassword = _configuration["SeedUsers:Moderator:Password"] ?? "Moderator@123456";
        var moderatorName = _configuration["SeedUsers:Moderator:Name"] ?? "moderator123";

        await CreateUserWithRoleAsync(adminEmail, adminPassword, adminName, AppRoles.Admin);
        await CreateUserWithRoleAsync(moderatorEmail, moderatorPassword, moderatorName, AppRoles.Moderator);
    }

    private async Task CreateUserWithRoleAsync(string email, string password, string name, string role)
    {
        var existingUser = await _userManager.FindByEmailAsync(email);
        if (existingUser != null)
        {
            _logger.LogInformation("User {Email} already exists, skipping creation", email);

            var currentRoles = await _userManager.GetRolesAsync(existingUser);
            if (!currentRoles.Contains(role))
            {
                await _userManager.AddToRoleAsync(existingUser, role);
                _logger.LogInformation("Added role {Role} to existing user {Email}", role, email);
            }

            if (role == AppRoles.Moderator && !existingUser.IsModerator)
            {
                existingUser.IsModerator = true;
                await _userManager.UpdateAsync(existingUser);
            }

            return;
        }

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            Name = name,
            EmailConfirmed = true,
            IsModerator = role == AppRoles.Moderator || role == AppRoles.Admin,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var createResult = await _userManager.CreateAsync(user, password);
        if (!createResult.Succeeded)
        {
            _logger.LogError("Failed to create {Role} user {Email}: {Errors}",
                role, email, string.Join(", ", createResult.Errors.Select(e => e.Description)));
            return;
        }

        var roleResult = await _userManager.AddToRoleAsync(user, role);
        if (roleResult.Succeeded)
        {
            _logger.LogInformation("{Role} user {Email} created successfully", role, email);
        }
        else
        {
            _logger.LogError("Failed to assign {Role} role to {Email}: {Errors}",
                role, email, string.Join(", ", roleResult.Errors.Select(e => e.Description)));
        }
    }
}
