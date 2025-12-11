using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Moq;
using SkillsBarter.Data;
using SkillsBarter.DTOs;
using SkillsBarter.Models;
using SkillsBarter.Services;
using SkillsBarter.Tests.TestUtils;
using Xunit;

namespace SkillsBarter.Tests.Services;

public class UserServiceTests
{
    private readonly Mock<UserManager<ApplicationUser>> _userManagerMock;
    private readonly Mock<ILogger<UserService>> _loggerMock = new();
    private ApplicationDbContext _context = null!;
    private UserService _userService = null!;

    public UserServiceTests()
    {
        _userManagerMock = IdentityMocks.CreateUserManager<ApplicationUser>();

        SetupInMemoryDatabase();
    }

    private void SetupInMemoryDatabase()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _context = new ApplicationDbContext(options);
        _userService = new UserService(_context, _userManagerMock.Object, _loggerMock.Object);
    }

    private async Task<(ApplicationUser user, Skill skill, Offer offer)> SeedUserAsync()
    {
        var category = new SkillCategory { Code = "TECH", Label = "Technology" };
        var skill = new Skill { Id = 1, Name = "C#", CategoryCode = category.Code, Category = category };

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Name = "Test User",
            UserName = "tester",
            Email = "test@example.com",
            ReputationScore = 4.5m,
            VerificationLevel = 2,
            CreatedAt = DateTime.UtcNow
        };

        var offer = new Offer
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            SkillId = skill.Id,
            Title = "Offer",
            Description = "Desc",
            StatusCode = OfferStatusCode.Active,
            Skill = skill,
            User = user
        };

        _context.SkillCategories.Add(category);
        _context.Skills.Add(skill);
        _context.Users.Add(user);
        _context.Offers.Add(offer);

        _context.UserSkills.Add(new UserSkill
        {
            UserId = user.Id,
            SkillId = skill.Id,
            AddedAt = DateTime.UtcNow
        });

        _context.Reviews.Add(new Review
        {
            Id = Guid.NewGuid(),
            ReviewerId = Guid.NewGuid(),
            RecipientId = user.Id,
            AgreementId = Guid.NewGuid(),
            Rating = 5,
            Body = "Great",
            Recipient = user,
            Reviewer = new ApplicationUser { Id = Guid.NewGuid(), UserName = "rev" }
        });

        await _context.SaveChangesAsync();
        return (user, skill, offer);
    }

    [Fact]
    public async Task GetPublicProfileAsync_UserNotFound_ReturnsNull()
    {
        var result = await _userService.GetPublicProfileAsync(Guid.NewGuid());
        Assert.Null(result);
    }

    [Fact]
    public async Task GetPublicProfileAsync_ReturnsAggregatedProfile()
    {
        var (user, skill, offer) = await SeedUserAsync();

        var result = await _userService.GetPublicProfileAsync(user.Id);

        Assert.NotNull(result);
        Assert.Equal(user.Id, result!.Id);
        Assert.Single(result.Skills);
        Assert.Equal(skill.Name, result.Skills.First().SkillName);
        Assert.Equal(offer.SkillId, result.Skills.First().SkillId);
        Assert.Equal(1, result.Stats.TotalActiveOffers);
        Assert.True(result.Stats.AverageRating >= 4.5m);
    }

    [Fact]
    public async Task GetDetailedProfileAsync_UserNotFound_ReturnsNull()
    {
        var result = await _userService.GetDetailedProfileAsync(Guid.NewGuid());
        Assert.Null(result);
    }

    [Fact]
    public async Task GetDetailedProfileAsync_ReturnsRolesAndDetails()
    {
        var (user, skill, _) = await SeedUserAsync();
        _userManagerMock.Setup(u => u.GetRolesAsync(It.Is<ApplicationUser>(a => a.Id == user.Id)))
            .ReturnsAsync(new List<string> { "Role1" });

        var result = await _userService.GetDetailedProfileAsync(user.Id);

        Assert.NotNull(result);
        Assert.Contains("Role1", result!.Roles);
        Assert.Equal(skill.Id, result.Skills.First().SkillId);
    }

    [Fact]
    public async Task UpdateProfileAsync_UserNotFound_ReturnsNull()
    {
        var result = await _userService.UpdateProfileAsync(Guid.NewGuid(), new UpdateProfileRequest());
        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateProfileAsync_InvalidSkillIds_Throws()
    {
        var (user, _, _) = await SeedUserAsync();
        var request = new UpdateProfileRequest { SkillIds = new List<int> { 999 } };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _userService.UpdateProfileAsync(user.Id, request));
    }

    [Fact]
    public async Task UpdateProfileAsync_UpdatesFieldsAndSkills()
    {
        var (user, skill, _) = await SeedUserAsync();
        var newSkill = new Skill { Id = 5, Name = "SQL", CategoryCode = "TECH" };
        _context.Skills.Add(newSkill);
        await _context.SaveChangesAsync();

        var request = new UpdateProfileRequest
        {
            Name = "Updated",
            Description = "Desc",
            PhoneNumber = "123",
            SkillIds = new List<int> { newSkill.Id }
        };

        _userManagerMock.Setup(u => u.GetRolesAsync(It.Is<ApplicationUser>(a => a.Id == user.Id)))
            .ReturnsAsync(new List<string> { "Role1" });

        var result = await _userService.UpdateProfileAsync(user.Id, request);

        Assert.NotNull(result);
        Assert.Equal("Updated", result!.Name);
        Assert.Contains(newSkill.Id, result.Skills.Select(s => s.SkillId));

        var storedUser = await _context.Users.Include(u => u.UserSkills).FirstAsync(u => u.Id == user.Id);
        Assert.Equal("Updated", storedUser.Name);
        Assert.Single(storedUser.UserSkills);
        Assert.Equal(newSkill.Id, storedUser.UserSkills.First().SkillId);
    }
}
