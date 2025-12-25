using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using SkillsBarter.Constants;
using SkillsBarter.Data;
using SkillsBarter.DTOs;
using SkillsBarter.Models;
using SkillsBarter.Services;
using Xunit;

namespace SkillsBarter.Tests.Services;

public class AgreementServiceTests
{
    private readonly Mock<INotificationService> _notificationServiceMock = new();
    private readonly Mock<ILogger<AgreementService>> _loggerMock = new();
    private ApplicationDbContext _context = null!;
    private AgreementService _agreementService = null!;

    public AgreementServiceTests()
    {
        SetupInMemoryDatabase();
    }

    private void SetupInMemoryDatabase()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _context = new ApplicationDbContext(options);
        _agreementService = new AgreementService(
            _context,
            _notificationServiceMock.Object,
            _loggerMock.Object
        );
    }

    private async Task<(ApplicationUser requester, ApplicationUser provider, Offer offer, Skill skill)> SeedOfferAsync(
        OfferStatusCode status = OfferStatusCode.Active,
        Guid? offerOwnerId = null)
    {
        var requester = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Name = "Requester",
            UserName = "requester",
            Email = "requester@example.com"
        };
        var provider = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Name = "Provider",
            UserName = "provider",
            Email = "provider@example.com"
        };

        var category = new SkillCategory { Code = "TECH", Label = "Technology" };
        var skill = new Skill { Name = "Programming", CategoryCode = category.Code };

        var offerStatuses = new[]
        {
            new OfferStatus { Code = OfferStatusCode.Active, Label = "Active" },
            new OfferStatus { Code = OfferStatusCode.UnderAgreement, Label = "Under Agreement" },
            new OfferStatus { Code = OfferStatusCode.Completed, Label = "Completed" }
        };

        _context.Users.AddRange(requester, provider);
        _context.SkillCategories.Add(category);
        _context.Skills.Add(skill);
        _context.OfferStatuses.AddRange(offerStatuses);

        var offer = new Offer
        {
            Id = Guid.NewGuid(),
            UserId = offerOwnerId ?? requester.Id,
            SkillId = skill.Id,
            Title = "Sample Offer",
            Description = "Sample Description",
            StatusCode = status,
            User = requester,
            Skill = skill
        };

        _context.Offers.Add(offer);
        await _context.SaveChangesAsync();

        return (requester, provider, offer, skill);
    }

    [Fact]
    public async Task CreateAgreementAsync_WithValidData_CreatesAgreementAndUpdatesOffer()
    {
        var (requester, provider, offer, _) = await SeedOfferAsync();

        offer.UserId = provider.Id;
        offer.User = provider;
        _context.Offers.Update(offer);
        await _context.SaveChangesAsync();

        var milestones = new List<CreateMilestoneRequest>
        {
            new CreateMilestoneRequest
            {
                Title = "Phase 1",
                DurationInDays = 7,
                DueAt = DateTime.UtcNow.AddDays(7)
            },
            new CreateMilestoneRequest
            {
                Title = "Phase 2",
                DurationInDays = 14,
                DueAt = DateTime.UtcNow.AddDays(14)
            }
        };

        var result = await _agreementService.CreateAgreementAsync(
            offer.Id,
            requester.Id,
            provider.Id,
            "Terms",
            milestones
        );

        Assert.NotNull(result);
        Assert.Equal(requester.Id, result!.RequesterId);
        Assert.Equal(provider.Id, result.ProviderId);
        Assert.Equal(AgreementStatus.InProgress, result.Status);

        var storedOffer = await _context.Offers.FindAsync(offer.Id);
        Assert.Equal(OfferStatusCode.UnderAgreement, storedOffer!.StatusCode);

        _notificationServiceMock.Verify(
            n => n.CreateAsync(requester.Id, NotificationType.AgreementCreated, "Agreement Proposal Sent", It.IsAny<string>(), It.IsAny<Guid?>()),
            Times.Once);
        _notificationServiceMock.Verify(
            n => n.CreateAsync(provider.Id, NotificationType.AgreementCreated, "New Agreement Proposal", It.IsAny<string>(), It.IsAny<Guid?>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateAgreementAsync_WhenOfferNotFound_ReturnsNull()
    {
        var result = await _agreementService.CreateAgreementAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Terms"
        );

        Assert.Null(result);
    }

    [Fact]
    public async Task CreateAgreementAsync_WhenActiveAgreementExists_ReturnsNull()
    {
        var (requester, provider, offer, _) = await SeedOfferAsync();

        var activeAgreement = new Agreement
        {
            Id = Guid.NewGuid(),
            OfferId = offer.Id,
            RequesterId = requester.Id,
            ProviderId = provider.Id,
            Status = AgreementStatus.InProgress,
            CreatedAt = DateTime.UtcNow
        };

        _context.Agreements.Add(activeAgreement);
        await _context.SaveChangesAsync();

        var result = await _agreementService.CreateAgreementAsync(
            offer.Id,
            requester.Id,
            provider.Id,
            "Terms"
        );

        Assert.Null(result);
    }

    [Fact]
    public async Task CompleteAgreementAsync_WhenUserNotParticipant_ReturnsNull()
    {
        var (requester, provider, offer, _) = await SeedOfferAsync(OfferStatusCode.UnderAgreement);
        var agreement = new Agreement
        {
            Id = Guid.NewGuid(),
            OfferId = offer.Id,
            RequesterId = requester.Id,
            ProviderId = provider.Id,
            Status = AgreementStatus.InProgress,
            Offer = offer
        };
        _context.Agreements.Add(agreement);
        await _context.SaveChangesAsync();

        var outsiderId = Guid.NewGuid();

        var result = await _agreementService.CompleteAgreementAsync(agreement.Id, outsiderId);

        Assert.Null(result);
    }

    [Fact]
    public async Task CompleteAgreementAsync_UpdatesAgreementAndOfferAndNotifies()
    {
        var (requester, provider, offer, _) = await SeedOfferAsync(OfferStatusCode.UnderAgreement);
        var agreement = new Agreement
        {
            Id = Guid.NewGuid(),
            OfferId = offer.Id,
            RequesterId = requester.Id,
            ProviderId = provider.Id,
            Status = AgreementStatus.InProgress,
            Offer = offer
        };
        _context.Agreements.Add(agreement);
        await _context.SaveChangesAsync();

        var result = await _agreementService.CompleteAgreementAsync(agreement.Id, requester.Id);

        Assert.NotNull(result);
        Assert.Equal(AgreementStatus.Completed, result!.Status);

        var storedAgreement = await _context.Agreements.FindAsync(agreement.Id);
        Assert.Equal(AgreementStatus.Completed, storedAgreement!.Status);
        Assert.NotNull(storedAgreement.CompletedAt);

        var storedOffer = await _context.Offers.FindAsync(offer.Id);
        Assert.Equal(OfferStatusCode.Completed, storedOffer!.StatusCode);

        _notificationServiceMock.Verify(
            n => n.CreateAsync(provider.Id, NotificationType.AgreementCompleted, "Agreement Completed", It.IsAny<string>(), It.IsAny<Guid?>()),
            Times.Once);
    }

    [Fact]
    public async Task GetAgreementDetailByIdAsync_ReturnsMappedDetail()
    {
        var (requester, provider, offer, skill) = await SeedOfferAsync();

        var agreementId = Guid.NewGuid();
        var agreement = new Agreement
        {
            Id = agreementId,
            OfferId = offer.Id,
            RequesterId = requester.Id,
            ProviderId = provider.Id,
            Status = AgreementStatus.InProgress,
            Offer = offer,
            Requester = requester,
            Provider = provider
        };

        var milestone = new Milestone
        {
            Id = Guid.NewGuid(),
            AgreementId = agreementId,
            Agreement = agreement,
            Title = "Milestone 1",
            DurationInDays = 14,
            Status = MilestoneStatus.Pending
        };

        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            AgreementId = agreementId,
            Agreement = agreement,
            Amount = 50,
            Currency = "EUR",
            PaymentType = "Card",
            Status = "Pending",
            TipFromUserId = requester.Id,
            TipFromUser = requester,
            TipToUserId = provider.Id,
            TipToUser = provider
        };

        var review = new Review
        {
            Id = Guid.NewGuid(),
            ReviewerId = requester.Id,
            Reviewer = requester,
            RecipientId = provider.Id,
            Recipient = provider,
            AgreementId = agreementId,
            Agreement = agreement,
            Rating = 5,
            Body = "Great work"
        };

        agreement.Milestones = new List<Milestone> { milestone };
        agreement.Payments = new List<Payment> { payment };
        agreement.Reviews = new List<Review> { review };

        offer.Skill = skill;
        _context.Agreements.Add(agreement);
        await _context.SaveChangesAsync();

        var result = await _agreementService.GetAgreementDetailByIdAsync(agreement.Id);

        Assert.NotNull(result);
        Assert.Equal(agreement.Id, result!.Id);
        Assert.Equal(requester.Id, result.RequesterId);
        Assert.Equal(provider.Id, result.ProviderId);
        Assert.Single(result.Milestones);
        Assert.Single(result.Payments);
        Assert.Single(result.Reviews);
        Assert.Equal(skill.Name, result.Offer.SkillName);
    }

    [Fact]
    public async Task ProcessAbandonedAgreementsAsync_CancelsAgreementsAndCreatesPenalties()
    {
        var (requester, provider, offer, _) = await SeedOfferAsync(OfferStatusCode.UnderAgreement);
        var oldDate = DateTime.UtcNow.AddDays(-(PenaltyConstants.AbandonmentDays + 1));

        var agreement = new Agreement
        {
            Id = Guid.NewGuid(),
            OfferId = offer.Id,
            RequesterId = requester.Id,
            ProviderId = provider.Id,
            Status = AgreementStatus.InProgress,
            CreatedAt = oldDate,
            Offer = offer,
            Deliverables = new List<Deliverable>()
        };

        _context.Agreements.Add(agreement);
        await _context.SaveChangesAsync();

        await _agreementService.ProcessAbandonedAgreementsAsync();

        var updated = await _context.Agreements.FindAsync(agreement.Id);
        Assert.Equal(AgreementStatus.Cancelled, updated!.Status);
        Assert.Equal(2, _context.Penalties.Count());
    }
}
