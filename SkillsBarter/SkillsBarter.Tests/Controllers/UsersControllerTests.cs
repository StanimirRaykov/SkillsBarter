using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using SkillsBarter.Constants;
using SkillsBarter.Controllers;
using SkillsBarter.Data;
using SkillsBarter.DTOs;
using SkillsBarter.Models;
using SkillsBarter.Services;
using SkillsBarter.Tests.TestUtils;
using Xunit;

namespace SkillsBarter.Tests.Controllers;

public class UsersControllerTests
{
    private readonly Mock<IUserService> _userServiceMock = new();
    private readonly Mock<IReviewService> _reviewServiceMock = new();
    private readonly Mock<UserManager<ApplicationUser>> _userManagerMock;
    private readonly Mock<RoleManager<IdentityRole<Guid>>> _roleManagerMock;
    private readonly Mock<IConfiguration> _configurationMock = new();
    private readonly Mock<ILogger<RoleSeeder>> _roleLoggerMock = new();
    private readonly RoleSeeder _roleSeeder;
    private readonly Mock<ILogger<UsersController>> _loggerMock = new();
    private readonly ApplicationDbContext _dbContext;
    private readonly UsersController _controller;
    private readonly ApplicationUser _currentUser;

    public UsersControllerTests()
    {
        _userManagerMock = IdentityMocks.CreateUserManager<ApplicationUser>();
        _roleManagerMock = IdentityMocks.CreateRoleManager<IdentityRole<Guid>>();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new ApplicationDbContext(options);

        _currentUser = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = "current",
            Name = "Current User",
            Email = "current@example.com"
        };

        _roleSeeder = new RoleSeeder(
            _roleManagerMock.Object,
            _userManagerMock.Object,
            _configurationMock.Object,
            _roleLoggerMock.Object
        );

        _controller = new UsersController(
            _userServiceMock.Object,
            _reviewServiceMock.Object,
            _userManagerMock.Object,
            _dbContext,
            _roleSeeder,
            _loggerMock.Object
        );

        SetupControllerContext();
    }

    private void SetupControllerContext()
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, _currentUser.Id.ToString()),
            new(ClaimTypes.Name, _currentUser.UserName!)
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var claimsPrincipal = new ClaimsPrincipal(identity);

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = claimsPrincipal }
        };
    }

    [Fact]
    public async Task GetUserProfile_InvalidId_ReturnsBadRequest()
    {
        var result = await _controller.GetUserProfile(Guid.Empty);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Invalid user ID", GetMessage(badRequest.Value));
    }

    [Fact]
    public async Task GetUserProfile_NotFound_ReturnsNotFound()
    {
        var userId = Guid.NewGuid();
        _userServiceMock.Setup(s => s.GetPublicProfileAsync(userId))
            .ReturnsAsync((PublicUserProfileResponse?)null);

        var result = await _controller.GetUserProfile(userId);

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal("User not found", GetMessage(notFound.Value));
    }

    [Fact]
    public async Task GetUserProfile_Success_ReturnsOk()
    {
        var userId = Guid.NewGuid();
        var profile = new PublicUserProfileResponse { Id = userId, Name = "Test" };
        _userServiceMock.Setup(s => s.GetPublicProfileAsync(userId))
            .ReturnsAsync(profile);

        var result = await _controller.GetUserProfile(userId);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(profile, ok.Value);
    }

    [Fact]
    public async Task GetAllUsers_ReturnsOkWithRoles()
    {
        var user = new ApplicationUser { Id = Guid.NewGuid(), Name = "A", Email = "a@e.com" };
        var users = new List<ApplicationUser> { user };
        _userManagerMock.SetupGet(u => u.Users).Returns(users.AsQueryable());
        _userManagerMock.Setup(u => u.GetRolesAsync(user))
            .ReturnsAsync(new List<string> { AppRoles.Admin });

        var result = await _controller.GetAllUsers();

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsAssignableFrom<IEnumerable<object>>(ok.Value);
        Assert.Single(payload);
    }

    [Fact]
    public async Task AssignRole_InvalidRole_ReturnsBadRequest()
    {
        var result = await _controller.AssignRole(Guid.NewGuid(), new AssignRoleRequest { RoleName = "invalid" });

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("Invalid role", GetMessage(badRequest.Value));
    }

    [Fact]
    public async Task AssignRole_Success_ReturnsOk()
    {
        var userId = Guid.NewGuid();
        var user = new ApplicationUser { Id = userId };
        _userManagerMock.Setup(u => u.FindByIdAsync(userId.ToString()))
            .ReturnsAsync(user);
        _roleManagerMock.Setup(r => r.RoleExistsAsync(AppRoles.Admin))
            .ReturnsAsync(true);
        _userManagerMock.Setup(u => u.GetRolesAsync(user))
            .ReturnsAsync(new List<string>());
        _userManagerMock.Setup(u => u.RemoveFromRolesAsync(user, It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(IdentityResult.Success);
        _userManagerMock.Setup(u => u.AddToRoleAsync(user, AppRoles.Admin))
            .ReturnsAsync(IdentityResult.Success);

        var result = await _controller.AssignRole(userId, new AssignRoleRequest { RoleName = AppRoles.Admin });

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal($"User assigned to role {AppRoles.Admin} successfully", GetMessage(ok.Value));
    }

    [Fact]
    public async Task GetUserDetails_NotOwnerNotModerator_ReturnsForbidden()
    {
        var targetId = Guid.NewGuid();
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(_currentUser);
        _userManagerMock.Setup(u => u.GetRolesAsync(_currentUser))
            .ReturnsAsync(new List<string>());

        var result = await _controller.GetUserDetails(targetId);

        var forbidden = Assert.IsType<ObjectResult>(result);
        Assert.Equal(403, forbidden.StatusCode);
    }

    [Fact]
    public async Task GetUserDetails_Success_ReturnsOk()
    {
        var targetId = _currentUser.Id;
        var detailedProfile = new DetailedUserProfileResponse { Id = targetId, Name = "Me" };

        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(_currentUser);
        _userManagerMock.Setup(u => u.GetRolesAsync(_currentUser))
            .ReturnsAsync(new List<string> { AppRoles.Freemium });
        _userServiceMock.Setup(s => s.GetDetailedProfileAsync(targetId))
            .ReturnsAsync(detailedProfile);

        var result = await _controller.GetUserDetails(targetId);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.True((bool?)ok.Value?.GetType().GetProperty("success")?.GetValue(ok.Value));
        Assert.Equal(detailedProfile, ok.Value?.GetType().GetProperty("profile")?.GetValue(ok.Value));
    }

    [Fact]
    public async Task GetUserReviews_NotFound_ReturnsNotFound()
    {
        var userId = Guid.NewGuid();
        _userManagerMock.Setup(u => u.FindByIdAsync(userId.ToString()))
            .ReturnsAsync((ApplicationUser?)null);

        var result = await _controller.GetUserReviews(userId, 1, 10);

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal("User not found", GetMessage(notFound.Value));
    }

    [Fact]
    public async Task GetUserReviews_Success_ReturnsOk()
    {
        var userId = Guid.NewGuid();
        var reviews = new UserReviewsWithSummaryResponse
        {
            Summary = new ReviewSummary { TotalReviews = 1, AverageRating = 5 },
            Reviews = new PaginatedResponse<ReviewResponse> { Items = new List<ReviewResponse>() }
        };

        _userManagerMock.Setup(u => u.FindByIdAsync(userId.ToString()))
            .ReturnsAsync(new ApplicationUser { Id = userId });
        _reviewServiceMock.Setup(r => r.GetUserReviewsWithSummaryAsync(userId, 1, 10))
            .ReturnsAsync(reviews);

        var result = await _controller.GetUserReviews(userId, 1, 10);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(reviews, ok.Value);
    }

    [Fact]
    public async Task AddUserSkill_SkillNotFound_ReturnsBadRequest()
    {
        var userId = _currentUser.Id;
        var request = new AddSkillRequest { SkillId = 5 };

        var result = await _controller.AddUserSkill(userId, request);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Skill not found", GetMessage(badRequest.Value));
    }

    [Fact]
    public async Task AddUserSkill_Success_AddsSkill()
    {
        var userId = _currentUser.Id;
        var skill = new Skill { Id = 1, Name = "C#", CategoryCode = "TECH" };
        _dbContext.Skills.Add(skill);
        await _dbContext.SaveChangesAsync();

        var request = new AddSkillRequest { SkillId = skill.Id };

        var result = await _controller.AddUserSkill(userId, request);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal("Skill added successfully", GetMessage(ok.Value));
        Assert.True(await _dbContext.UserSkills.AnyAsync(us => us.UserId == userId && us.SkillId == skill.Id));
    }

    [Fact]
    public async Task RemoveUserSkill_NotFound_ReturnsNotFound()
    {
        var result = await _controller.RemoveUserSkill(_currentUser.Id, 99);

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal("User skill not found", GetMessage(notFound.Value));
    }

    [Fact]
    public async Task RemoveUserSkill_Success_RemovesSkill()
    {
        var skill = new Skill { Id = 2, Name = "SQL", CategoryCode = "TECH" };
        _dbContext.Skills.Add(skill);
        _dbContext.UserSkills.Add(new UserSkill
        {
            UserId = _currentUser.Id,
            SkillId = skill.Id,
            AddedAt = DateTime.UtcNow
        });
        await _dbContext.SaveChangesAsync();

        var result = await _controller.RemoveUserSkill(_currentUser.Id, skill.Id);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal("Skill removed successfully", GetMessage(ok.Value));
        Assert.False(await _dbContext.UserSkills.AnyAsync(us => us.UserId == _currentUser.Id && us.SkillId == skill.Id));
    }

    [Fact]
    public async Task GetMyProfile_NotFound_ReturnsNotFound()
    {
        _userServiceMock.Setup(s => s.GetDetailedProfileAsync(_currentUser.Id))
            .ReturnsAsync((DetailedUserProfileResponse?)null);

        var result = await _controller.GetMyProfile();

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal("Profile not found", GetMessage(notFound.Value));
    }

    [Fact]
    public async Task UpdateMyProfile_InvalidModel_ReturnsBadRequest()
    {
        _controller.ModelState.AddModelError("Name", "Required");

        var result = await _controller.UpdateMyProfile(new UpdateProfileRequest());

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Invalid input", GetMessage(badRequest.Value));
    }

    [Fact]
    public async Task UpdateMyProfile_Success_ReturnsOk()
    {
        var request = new UpdateProfileRequest { Name = "Updated" };
        var updated = new DetailedUserProfileResponse { Id = _currentUser.Id, Name = "Updated" };

        _userServiceMock.Setup(s => s.UpdateProfileAsync(_currentUser.Id, request))
            .ReturnsAsync(updated);

        var result = await _controller.UpdateMyProfile(request);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.True((bool?)ok.Value?.GetType().GetProperty("success")?.GetValue(ok.Value));
    }

    private static string? GetMessage(object? value) =>
        value?.GetType().GetProperty("message")?.GetValue(value)?.ToString();
}
