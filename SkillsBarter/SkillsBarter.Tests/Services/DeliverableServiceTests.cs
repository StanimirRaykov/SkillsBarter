using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Moq;
using SkillsBarter.Constants;
using SkillsBarter.Data;
using SkillsBarter.DTOs;
using SkillsBarter.Models;
using SkillsBarter.Services;
using Xunit;

namespace SkillsBarter.Tests.Services;

public class DeliverableServiceTests
{
    private readonly Mock<INotificationService> _notificationServiceMock = new();
    private readonly Mock<ILogger<DeliverableService>> _loggerMock = new();
    private ApplicationDbContext _context = null!;
    private DeliverableService _deliverableService = null!;

    public DeliverableServiceTests()
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
        _deliverableService = new DeliverableService(
            _context,
            _notificationServiceMock.Object,
            _loggerMock.Object
        );
    }

    private async Task<(ApplicationUser requester, ApplicationUser provider, Agreement agreement)> SeedAgreementAsync(
        AgreementStatus status = AgreementStatus.InProgress)
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
        var skill = new Skill { Id = 1, Name = "Programming", CategoryCode = category.Code, Category = category };

        var offerStatuses = new[]
        {
            new OfferStatus { Code = OfferStatusCode.Active, Label = "Active" },
            new OfferStatus { Code = OfferStatusCode.UnderAgreement, Label = "Under Agreement" }
        };

        _context.Users.AddRange(requester, provider);
        _context.SkillCategories.Add(category);
        _context.Skills.Add(skill);
        _context.OfferStatuses.AddRange(offerStatuses);

        var offer = new Offer
        {
            Id = Guid.NewGuid(),
            UserId = requester.Id,
            SkillId = skill.Id,
            Title = "Test Offer",
            Description = "Test Description",
            StatusCode = OfferStatusCode.UnderAgreement,
            User = requester,
            Skill = skill
        };
        _context.Offers.Add(offer);

        var agreement = new Agreement
        {
            Id = Guid.NewGuid(),
            OfferId = offer.Id,
            RequesterId = requester.Id,
            ProviderId = provider.Id,
            Status = status,
            CreatedAt = DateTime.UtcNow,
            Requester = requester,
            Provider = provider,
            Offer = offer
        };
        _context.Agreements.Add(agreement);

        await _context.SaveChangesAsync();

        return (requester, provider, agreement);
    }

    [Fact]
    public async Task SubmitDeliverableAsync_AgreementNotFound_ReturnsNull()
    {
        var request = new SubmitDeliverableRequest
        {
            AgreementId = Guid.NewGuid(),
            Link = "http://deliverable.com/file",
            Description = "Test deliverable"
        };

        var result = await _deliverableService.SubmitDeliverableAsync(request, Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task SubmitDeliverableAsync_UserNotPartOfAgreement_ReturnsNull()
    {
        var (_, _, agreement) = await SeedAgreementAsync();

        var outsider = new ApplicationUser { Id = Guid.NewGuid(), UserName = "outsider" };
        _context.Users.Add(outsider);
        await _context.SaveChangesAsync();

        var request = new SubmitDeliverableRequest
        {
            AgreementId = agreement.Id,
            Link = "http://deliverable.com/file",
            Description = "Test deliverable"
        };

        var result = await _deliverableService.SubmitDeliverableAsync(request, outsider.Id);

        Assert.Null(result);
    }

    [Fact]
    public async Task SubmitDeliverableAsync_AgreementNotInProgress_ReturnsNull()
    {
        var (requester, _, agreement) = await SeedAgreementAsync(AgreementStatus.Completed);

        var request = new SubmitDeliverableRequest
        {
            AgreementId = agreement.Id,
            Link = "http://deliverable.com/file",
            Description = "Test deliverable"
        };

        var result = await _deliverableService.SubmitDeliverableAsync(request, requester.Id);

        Assert.Null(result);
    }

    [Fact]
    public async Task SubmitDeliverableAsync_AlreadySubmitted_ReturnsNull()
    {
        var (requester, _, agreement) = await SeedAgreementAsync();

        var existingDeliverable = new Deliverable
        {
            Id = Guid.NewGuid(),
            AgreementId = agreement.Id,
            SubmittedById = requester.Id,
            Link = "http://existing.com",
            Description = "Existing",
            Status = DeliverableStatus.Submitted
        };
        _context.Deliverables.Add(existingDeliverable);
        await _context.SaveChangesAsync();

        var request = new SubmitDeliverableRequest
        {
            AgreementId = agreement.Id,
            Link = "http://new.com",
            Description = "New deliverable"
        };

        var result = await _deliverableService.SubmitDeliverableAsync(request, requester.Id);

        Assert.Null(result);
    }

    [Fact]
    public async Task SubmitDeliverableAsync_Success_CreatesDeliverableAndNotifies()
    {
        var (requester, provider, agreement) = await SeedAgreementAsync();

        var request = new SubmitDeliverableRequest
        {
            AgreementId = agreement.Id,
            Link = "http://deliverable.com/file",
            Description = "Test deliverable"
        };

        var result = await _deliverableService.SubmitDeliverableAsync(request, requester.Id);

        Assert.NotNull(result);
        Assert.Equal(request.Link, result!.Link);
        Assert.Equal(DeliverableStatus.Submitted, result.Status);
        Assert.Equal(requester.Id, result.SubmittedById);

        var storedDeliverable = await _context.Deliverables.FirstOrDefaultAsync(d => d.AgreementId == agreement.Id);
        Assert.NotNull(storedDeliverable);

        _notificationServiceMock.Verify(
            n => n.CreateAsync(provider.Id, NotificationType.DeliverableSubmitted, It.IsAny<string>(), It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public async Task ApproveDeliverableAsync_DeliverableNotFound_ReturnsNull()
    {
        var result = await _deliverableService.ApproveDeliverableAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task ApproveDeliverableAsync_UserCannotApprove_ReturnsNull()
    {
        var (requester, _, agreement) = await SeedAgreementAsync();

        var deliverable = new Deliverable
        {
            Id = Guid.NewGuid(),
            AgreementId = agreement.Id,
            SubmittedById = requester.Id,
            Link = "http://deliverable.com",
            Description = "Test",
            Status = DeliverableStatus.Submitted,
            Agreement = agreement,
            SubmittedBy = requester
        };
        _context.Deliverables.Add(deliverable);
        await _context.SaveChangesAsync();

        var result = await _deliverableService.ApproveDeliverableAsync(deliverable.Id, requester.Id);

        Assert.Null(result);
    }

    [Fact]
    public async Task ApproveDeliverableAsync_NotSubmittedStatus_ReturnsNull()
    {
        var (requester, provider, agreement) = await SeedAgreementAsync();

        var deliverable = new Deliverable
        {
            Id = Guid.NewGuid(),
            AgreementId = agreement.Id,
            SubmittedById = requester.Id,
            Link = "http://deliverable.com",
            Description = "Test",
            Status = DeliverableStatus.Approved,
            Agreement = agreement,
            SubmittedBy = requester
        };
        _context.Deliverables.Add(deliverable);
        await _context.SaveChangesAsync();

        var result = await _deliverableService.ApproveDeliverableAsync(deliverable.Id, provider.Id);

        Assert.Null(result);
    }

    [Fact]
    public async Task ApproveDeliverableAsync_Success_ApprovesAndNotifies()
    {
        var (requester, provider, agreement) = await SeedAgreementAsync();

        var deliverable = new Deliverable
        {
            Id = Guid.NewGuid(),
            AgreementId = agreement.Id,
            SubmittedById = requester.Id,
            Link = "http://deliverable.com",
            Description = "Test",
            Status = DeliverableStatus.Submitted,
            Agreement = agreement,
            SubmittedBy = requester
        };
        _context.Deliverables.Add(deliverable);
        await _context.SaveChangesAsync();

        var result = await _deliverableService.ApproveDeliverableAsync(deliverable.Id, provider.Id);

        Assert.NotNull(result);
        Assert.Equal(DeliverableStatus.Approved, result!.Status);
        Assert.NotNull(result.ApprovedAt);

        _notificationServiceMock.Verify(
            n => n.CreateAsync(requester.Id, NotificationType.DeliverableApproved, It.IsAny<string>(), It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public async Task ApproveDeliverableAsync_BothApproved_CompletesAgreement()
    {
        var (requester, provider, agreement) = await SeedAgreementAsync();

        var requesterDeliverable = new Deliverable
        {
            Id = Guid.NewGuid(),
            AgreementId = agreement.Id,
            SubmittedById = requester.Id,
            Link = "http://requester.com",
            Description = "Requester deliverable",
            Status = DeliverableStatus.Approved,
            Agreement = agreement,
            SubmittedBy = requester
        };

        var providerDeliverable = new Deliverable
        {
            Id = Guid.NewGuid(),
            AgreementId = agreement.Id,
            SubmittedById = provider.Id,
            Link = "http://provider.com",
            Description = "Provider deliverable",
            Status = DeliverableStatus.Submitted,
            Agreement = agreement,
            SubmittedBy = provider
        };

        _context.Deliverables.AddRange(requesterDeliverable, providerDeliverable);
        await _context.SaveChangesAsync();

        var result = await _deliverableService.ApproveDeliverableAsync(providerDeliverable.Id, requester.Id);

        Assert.NotNull(result);
        Assert.Equal(DeliverableStatus.Approved, result!.Status);

        var storedAgreement = await _context.Agreements.FindAsync(agreement.Id);
        Assert.Equal(AgreementStatus.Completed, storedAgreement!.Status);
        Assert.NotNull(storedAgreement.CompletedAt);
    }

    [Fact]
    public async Task RequestRevisionAsync_DeliverableNotFound_ReturnsNull()
    {
        var request = new RequestRevisionRequest { Reason = "Needs improvement" };

        var result = await _deliverableService.RequestRevisionAsync(Guid.NewGuid(), request, Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task RequestRevisionAsync_UserCannotRequest_ReturnsNull()
    {
        var (requester, _, agreement) = await SeedAgreementAsync();

        var deliverable = new Deliverable
        {
            Id = Guid.NewGuid(),
            AgreementId = agreement.Id,
            SubmittedById = requester.Id,
            Link = "http://deliverable.com",
            Description = "Test",
            Status = DeliverableStatus.Submitted,
            Agreement = agreement,
            SubmittedBy = requester
        };
        _context.Deliverables.Add(deliverable);
        await _context.SaveChangesAsync();

        var request = new RequestRevisionRequest { Reason = "Needs improvement" };

        var result = await _deliverableService.RequestRevisionAsync(deliverable.Id, request, requester.Id);

        Assert.Null(result);
    }

    [Fact]
    public async Task RequestRevisionAsync_Success_RequestsRevisionAndNotifies()
    {
        var (requester, provider, agreement) = await SeedAgreementAsync();

        var deliverable = new Deliverable
        {
            Id = Guid.NewGuid(),
            AgreementId = agreement.Id,
            SubmittedById = requester.Id,
            Link = "http://deliverable.com",
            Description = "Test",
            Status = DeliverableStatus.Submitted,
            RevisionCount = 0,
            Agreement = agreement,
            SubmittedBy = requester
        };
        _context.Deliverables.Add(deliverable);
        await _context.SaveChangesAsync();

        var request = new RequestRevisionRequest { Reason = "Needs improvement" };

        var result = await _deliverableService.RequestRevisionAsync(deliverable.Id, request, provider.Id);

        Assert.NotNull(result);
        Assert.Equal(DeliverableStatus.RevisionRequested, result!.Status);
        Assert.Equal(request.Reason, result.RevisionReason);
        Assert.Equal(1, result.RevisionCount);

        _notificationServiceMock.Verify(
            n => n.CreateAsync(requester.Id, NotificationType.RevisionRequested, It.IsAny<string>(), It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public async Task ResubmitDeliverableAsync_DeliverableNotFound_ReturnsNull()
    {
        var request = new SubmitDeliverableRequest
        {
            AgreementId = Guid.NewGuid(),
            Link = "http://revised.com",
            Description = "Revised"
        };

        var result = await _deliverableService.ResubmitDeliverableAsync(Guid.NewGuid(), request, Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task ResubmitDeliverableAsync_UserNotSubmitter_ReturnsNull()
    {
        var (requester, provider, agreement) = await SeedAgreementAsync();

        var deliverable = new Deliverable
        {
            Id = Guid.NewGuid(),
            AgreementId = agreement.Id,
            SubmittedById = requester.Id,
            Link = "http://deliverable.com",
            Description = "Test",
            Status = DeliverableStatus.RevisionRequested,
            Agreement = agreement,
            SubmittedBy = requester
        };
        _context.Deliverables.Add(deliverable);
        await _context.SaveChangesAsync();

        var request = new SubmitDeliverableRequest
        {
            AgreementId = agreement.Id,
            Link = "http://revised.com",
            Description = "Revised"
        };

        var result = await _deliverableService.ResubmitDeliverableAsync(deliverable.Id, request, provider.Id);

        Assert.Null(result);
    }

    [Fact]
    public async Task ResubmitDeliverableAsync_NotRevisionRequestedStatus_ReturnsNull()
    {
        var (requester, _, agreement) = await SeedAgreementAsync();

        var deliverable = new Deliverable
        {
            Id = Guid.NewGuid(),
            AgreementId = agreement.Id,
            SubmittedById = requester.Id,
            Link = "http://deliverable.com",
            Description = "Test",
            Status = DeliverableStatus.Submitted,
            Agreement = agreement,
            SubmittedBy = requester
        };
        _context.Deliverables.Add(deliverable);
        await _context.SaveChangesAsync();

        var request = new SubmitDeliverableRequest
        {
            AgreementId = agreement.Id,
            Link = "http://revised.com",
            Description = "Revised"
        };

        var result = await _deliverableService.ResubmitDeliverableAsync(deliverable.Id, request, requester.Id);

        Assert.Null(result);
    }

    [Fact]
    public async Task ResubmitDeliverableAsync_Success_ResubmitsAndNotifies()
    {
        var (requester, provider, agreement) = await SeedAgreementAsync();

        var deliverable = new Deliverable
        {
            Id = Guid.NewGuid(),
            AgreementId = agreement.Id,
            SubmittedById = requester.Id,
            Link = "http://original.com",
            Description = "Original",
            Status = DeliverableStatus.RevisionRequested,
            RevisionReason = "Needs improvement",
            Agreement = agreement,
            SubmittedBy = requester
        };
        _context.Deliverables.Add(deliverable);
        await _context.SaveChangesAsync();

        var request = new SubmitDeliverableRequest
        {
            AgreementId = agreement.Id,
            Link = "http://revised.com",
            Description = "Revised deliverable"
        };

        var result = await _deliverableService.ResubmitDeliverableAsync(deliverable.Id, request, requester.Id);

        Assert.NotNull(result);
        Assert.Equal(request.Link, result!.Link);
        Assert.Equal(request.Description, result.Description);
        Assert.Equal(DeliverableStatus.Submitted, result.Status);
        Assert.Null(result.RevisionReason);

        _notificationServiceMock.Verify(
            n => n.CreateAsync(provider.Id, NotificationType.DeliverableSubmitted, It.IsAny<string>(), It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public async Task GetDeliverableByIdAsync_NotFound_ReturnsNull()
    {
        var result = await _deliverableService.GetDeliverableByIdAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task GetDeliverableByIdAsync_UserNotPartOfAgreement_ReturnsNull()
    {
        var (requester, _, agreement) = await SeedAgreementAsync();

        var deliverable = new Deliverable
        {
            Id = Guid.NewGuid(),
            AgreementId = agreement.Id,
            SubmittedById = requester.Id,
            Link = "http://deliverable.com",
            Description = "Test",
            Status = DeliverableStatus.Submitted,
            Agreement = agreement,
            SubmittedBy = requester
        };
        _context.Deliverables.Add(deliverable);
        await _context.SaveChangesAsync();

        var outsider = Guid.NewGuid();

        var result = await _deliverableService.GetDeliverableByIdAsync(deliverable.Id, outsider);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetDeliverableByIdAsync_Success_ReturnsDeliverable()
    {
        var (requester, provider, agreement) = await SeedAgreementAsync();

        var deliverable = new Deliverable
        {
            Id = Guid.NewGuid(),
            AgreementId = agreement.Id,
            SubmittedById = requester.Id,
            Link = "http://deliverable.com",
            Description = "Test",
            Status = DeliverableStatus.Submitted,
            Agreement = agreement,
            SubmittedBy = requester
        };
        _context.Deliverables.Add(deliverable);
        await _context.SaveChangesAsync();

        var result = await _deliverableService.GetDeliverableByIdAsync(deliverable.Id, provider.Id);

        Assert.NotNull(result);
        Assert.Equal(deliverable.Id, result!.Id);
        Assert.True(result.CanApprove);
        Assert.True(result.CanRequestRevision);
    }

    [Fact]
    public async Task GetAgreementDeliverablesAsync_AgreementNotFound_ReturnsNull()
    {
        var result = await _deliverableService.GetAgreementDeliverablesAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task GetAgreementDeliverablesAsync_UserNotPartOfAgreement_ReturnsNull()
    {
        var (_, _, agreement) = await SeedAgreementAsync();

        var outsider = Guid.NewGuid();

        var result = await _deliverableService.GetAgreementDeliverablesAsync(agreement.Id, outsider);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetAgreementDeliverablesAsync_Success_ReturnsDeliverables()
    {
        var (requester, provider, agreement) = await SeedAgreementAsync();

        var requesterDeliverable = new Deliverable
        {
            Id = Guid.NewGuid(),
            AgreementId = agreement.Id,
            SubmittedById = requester.Id,
            Link = "http://requester.com",
            Description = "Requester deliverable",
            Status = DeliverableStatus.Submitted,
            SubmittedBy = requester
        };

        var providerDeliverable = new Deliverable
        {
            Id = Guid.NewGuid(),
            AgreementId = agreement.Id,
            SubmittedById = provider.Id,
            Link = "http://provider.com",
            Description = "Provider deliverable",
            Status = DeliverableStatus.Submitted,
            SubmittedBy = provider
        };

        _context.Deliverables.AddRange(requesterDeliverable, providerDeliverable);
        await _context.SaveChangesAsync();

        var result = await _deliverableService.GetAgreementDeliverablesAsync(agreement.Id, requester.Id);

        Assert.NotNull(result);
        Assert.Equal(agreement.Id, result!.AgreementId);
        Assert.NotNull(result.RequesterDeliverable);
        Assert.NotNull(result.ProviderDeliverable);
        Assert.False(result.BothApproved);
    }

    [Fact]
    public async Task GetAgreementDeliverablesAsync_BothApproved_ReturnsBothApprovedTrue()
    {
        var (requester, provider, agreement) = await SeedAgreementAsync();

        var requesterDeliverable = new Deliverable
        {
            Id = Guid.NewGuid(),
            AgreementId = agreement.Id,
            SubmittedById = requester.Id,
            Link = "http://requester.com",
            Description = "Requester deliverable",
            Status = DeliverableStatus.Approved,
            SubmittedBy = requester
        };

        var providerDeliverable = new Deliverable
        {
            Id = Guid.NewGuid(),
            AgreementId = agreement.Id,
            SubmittedById = provider.Id,
            Link = "http://provider.com",
            Description = "Provider deliverable",
            Status = DeliverableStatus.Approved,
            SubmittedBy = provider
        };

        _context.Deliverables.AddRange(requesterDeliverable, providerDeliverable);
        await _context.SaveChangesAsync();

        var result = await _deliverableService.GetAgreementDeliverablesAsync(agreement.Id, requester.Id);

        Assert.NotNull(result);
        Assert.True(result!.BothApproved);
    }
}
