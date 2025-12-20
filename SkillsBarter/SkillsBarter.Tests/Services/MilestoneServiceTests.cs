using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using SkillsBarter.Data;
using SkillsBarter.DTOs;
using SkillsBarter.Models;
using SkillsBarter.Services;
using Xunit;

namespace SkillsBarter.Tests.Services;

public class MilestoneServiceTests
{
    private readonly Mock<ILogger<MilestoneService>> _mockLogger;
    private readonly Mock<INotificationService> _mockNotificationService;
    private ApplicationDbContext _context = null!;
    private MilestoneService _milestoneService = null!;

    public MilestoneServiceTests()
    {
        _mockLogger = new Mock<ILogger<MilestoneService>>();
        _mockNotificationService = new Mock<INotificationService>();
        SetupInMemoryDatabase();
    }

    private void SetupInMemoryDatabase()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        _milestoneService = new MilestoneService(_context, _mockNotificationService.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task CreateMilestoneAsync_ValidRequest_ReturnsMilestone()
    {
        var (agreement, _, _) = await SetupTestDataAsync();

        var request = new CreateMilestoneRequest
        {
            Title = "First Milestone",
            DurationInDays = 7,
            DueAt = DateTime.UtcNow.AddDays(7)
        };

        var result = await _milestoneService.CreateMilestoneAsync(agreement.Id, request);

        Assert.NotNull(result);
        Assert.Equal("First Milestone", result.Title);
        Assert.Equal(7, result.DurationInDays);
        Assert.Equal(MilestoneStatus.Pending, result.Status);
    }

    [Fact]
    public async Task GetMilestoneByIdAsync_ExistingMilestone_ReturnsMilestone()
    {
        var (agreement, milestone, _) = await SetupTestDataAsync();

        var result = await _milestoneService.GetMilestoneByIdAsync(milestone.Id);

        Assert.NotNull(result);
        Assert.Equal(milestone.Id, result.Id);
        Assert.Equal(milestone.Title, result.Title);
    }

    [Fact]
    public async Task GetMilestonesByAgreementIdAsync_ReturnsAllMilestones()
    {
        var (agreement, milestone, _) = await SetupTestDataAsync();

        var result = await _milestoneService.GetMilestonesByAgreementIdAsync(agreement.Id);

        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal(milestone.Id, result[0].Id);
    }

    [Fact]
    public async Task UpdateMilestoneAsync_ValidRequest_UpdatesMilestone()
    {
        var (agreement, milestone, _) = await SetupTestDataAsync();

        var updateRequest = new UpdateMilestoneRequest
        {
            Title = "Updated Milestone",
            DurationInDays = 14
        };

        var result = await _milestoneService.UpdateMilestoneAsync(milestone.Id, updateRequest);

        Assert.NotNull(result);
        Assert.Equal("Updated Milestone", result.Title);
        Assert.Equal(14, result.DurationInDays);
    }

    [Fact]
    public async Task DeleteMilestoneAsync_PendingMilestone_DeletesSuccessfully()
    {
        var (agreement, milestone, _) = await SetupTestDataAsync();

        var result = await _milestoneService.DeleteMilestoneAsync(milestone.Id);

        Assert.True(result);
        var deleted = await _context.Milestones.FindAsync(milestone.Id);
        Assert.Null(deleted);
    }

    [Fact]
    public async Task MarkMilestoneAsCompletedAsync_PendingMilestone_MarksAsCompleted()
    {
        var (agreement, milestone, users) = await SetupTestDataAsync();

        var result = await _milestoneService.MarkMilestoneAsCompletedAsync(milestone.Id);

        Assert.NotNull(result);
        Assert.Equal(MilestoneStatus.Completed, result.Status);

        _mockNotificationService.Verify(
            x => x.CreateAsync(
                It.IsAny<Guid>(),
                It.Is<string>(t => t == "milestone_completed"),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Guid?>()),
            Times.Exactly(2)
        );
    }

    private async Task<(Agreement agreement, Milestone milestone, (ApplicationUser requester, ApplicationUser provider) users)> SetupTestDataAsync()
    {
        var requester = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Name = "Requester User",
            UserName = "requester",
            Email = "requester@example.com"
        };
        _context.Users.Add(requester);

        var provider = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Name = "Provider User",
            UserName = "provider",
            Email = "provider@example.com"
        };
        _context.Users.Add(provider);

        var category = new SkillCategory { Code = "TECH", Label = "Technology" };
        _context.SkillCategories.Add(category);

        var skill = new Skill { Name = "C# Programming", CategoryCode = "TECH" };
        _context.Skills.Add(skill);

        var offer = new Offer
        {
            Id = Guid.NewGuid(),
            UserId = provider.Id,
            SkillId = skill.Id,
            Title = "Test Offer",
            Description = "Test Description",
            StatusCode = OfferStatusCode.UnderAgreement
        };
        _context.Offers.Add(offer);

        var agreement = new Agreement
        {
            Id = Guid.NewGuid(),
            OfferId = offer.Id,
            RequesterId = requester.Id,
            ProviderId = provider.Id,
            Terms = "Test terms",
            Status = AgreementStatus.InProgress,
            CreatedAt = DateTime.UtcNow
        };
        _context.Agreements.Add(agreement);

        var milestone = new Milestone
        {
            Id = Guid.NewGuid(),
            AgreementId = agreement.Id,
            Title = "Test Milestone",
            DurationInDays = 7,
            Status = MilestoneStatus.Pending,
            DueAt = DateTime.UtcNow.AddDays(7)
        };
        _context.Milestones.Add(milestone);

        await _context.SaveChangesAsync();

        return (agreement, milestone, (requester, provider));
    }
}
