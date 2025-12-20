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

public class DisputeServiceTests
{
    private readonly Mock<INotificationService> _notificationServiceMock = new();
    private readonly Mock<ILogger<DisputeService>> _loggerMock = new();
    private ApplicationDbContext _context = null!;
    private DisputeService _disputeService = null!;

    public DisputeServiceTests()
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
        _disputeService = new DisputeService(
            _context,
            _notificationServiceMock.Object,
            _loggerMock.Object
        );
    }

    private async Task<(ApplicationUser complainer, ApplicationUser respondent, Agreement agreement)> SeedAgreementAsync(
        AgreementStatus status = AgreementStatus.InProgress)
    {
        var complainer = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Name = "Complainer",
            UserName = "complainer",
            Email = "complainer@example.com"
        };

        var respondent = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Name = "Respondent",
            UserName = "respondent",
            Email = "respondent@example.com"
        };

        var category = new SkillCategory { Code = "TECH", Label = "Technology" };
        var skill = new Skill { Id = 1, Name = "Programming", CategoryCode = category.Code, Category = category };

        var offerStatuses = new[]
        {
            new OfferStatus { Code = OfferStatusCode.Active, Label = "Active" },
            new OfferStatus { Code = OfferStatusCode.UnderAgreement, Label = "Under Agreement" }
        };

        _context.Users.AddRange(complainer, respondent);
        _context.SkillCategories.Add(category);
        _context.Skills.Add(skill);
        _context.OfferStatuses.AddRange(offerStatuses);

        var offer = new Offer
        {
            Id = Guid.NewGuid(),
            UserId = complainer.Id,
            SkillId = skill.Id,
            Title = "Test Offer",
            Description = "Test Description",
            StatusCode = OfferStatusCode.UnderAgreement,
            User = complainer,
            Skill = skill
        };
        _context.Offers.Add(offer);

        var agreement = new Agreement
        {
            Id = Guid.NewGuid(),
            OfferId = offer.Id,
            RequesterId = complainer.Id,
            ProviderId = respondent.Id,
            Status = status,
            CreatedAt = DateTime.UtcNow,
            Requester = complainer,
            Provider = respondent,
            Offer = offer
        };
        _context.Agreements.Add(agreement);

        await _context.SaveChangesAsync();

        return (complainer, respondent, agreement);
    }

    [Fact]
    public async Task OpenDisputeAsync_AgreementNotFound_ReturnsNull()
    {
        var request = new OpenDisputeRequest
        {
            AgreementId = Guid.NewGuid(),
            ReasonCode = DisputeReasonCode.WorkNotDelivered,
            Description = "Test description for the dispute"
        };

        var result = await _disputeService.OpenDisputeAsync(request, Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task OpenDisputeAsync_UserNotPartOfAgreement_ReturnsNull()
    {
        var (_, _, agreement) = await SeedAgreementAsync();

        var outsider = new ApplicationUser { Id = Guid.NewGuid(), UserName = "outsider" };
        _context.Users.Add(outsider);
        await _context.SaveChangesAsync();

        var request = new OpenDisputeRequest
        {
            AgreementId = agreement.Id,
            ReasonCode = DisputeReasonCode.WorkNotDelivered,
            Description = "Test description for the dispute"
        };

        var result = await _disputeService.OpenDisputeAsync(request, outsider.Id);

        Assert.Null(result);
    }

    [Fact]
    public async Task OpenDisputeAsync_AgreementNotInProgress_ReturnsNull()
    {
        var (complainer, _, agreement) = await SeedAgreementAsync(AgreementStatus.Completed);

        var request = new OpenDisputeRequest
        {
            AgreementId = agreement.Id,
            ReasonCode = DisputeReasonCode.WorkNotDelivered,
            Description = "Test description for the dispute"
        };

        var result = await _disputeService.OpenDisputeAsync(request, complainer.Id);

        Assert.Null(result);
    }

    [Fact]
    public async Task OpenDisputeAsync_ActiveDisputeExists_ReturnsNull()
    {
        var (complainer, respondent, agreement) = await SeedAgreementAsync();

        var existingDispute = new Dispute
        {
            Id = Guid.NewGuid(),
            AgreementId = agreement.Id,
            OpenedById = complainer.Id,
            RespondentId = respondent.Id,
            ReasonCode = DisputeReasonCode.WorkNotDelivered,
            Description = "Existing dispute",
            Status = DisputeStatus.AwaitingResponse
        };
        _context.Disputes.Add(existingDispute);
        await _context.SaveChangesAsync();

        var request = new OpenDisputeRequest
        {
            AgreementId = agreement.Id,
            ReasonCode = DisputeReasonCode.QualityIssues,
            Description = "Another dispute attempt"
        };

        var result = await _disputeService.OpenDisputeAsync(request, complainer.Id);

        Assert.Null(result);
    }

    [Fact]
    public async Task OpenDisputeAsync_Success_CreatesDisputeAndUpdatesAgreement()
    {
        var (complainer, respondent, agreement) = await SeedAgreementAsync();

        var request = new OpenDisputeRequest
        {
            AgreementId = agreement.Id,
            ReasonCode = DisputeReasonCode.WorkNotDelivered,
            Description = "Test description for the dispute",
            Evidence = new List<EvidenceRequest>
            {
                new() { Link = "http://evidence.com/1", Description = "Evidence 1" }
            }
        };

        var result = await _disputeService.OpenDisputeAsync(request, complainer.Id);

        Assert.NotNull(result);
        Assert.Equal(complainer.Id, result!.Complainer.UserId);
        Assert.Equal(respondent.Id, result.Respondent.UserId);
        Assert.Equal(DisputeStatus.AwaitingResponse, result.Status);
        Assert.Single(result.Evidence);

        var storedAgreement = await _context.Agreements.FindAsync(agreement.Id);
        Assert.Equal(AgreementStatus.Disputed, storedAgreement!.Status);

        _notificationServiceMock.Verify(
            n => n.CreateAsync(respondent.Id, NotificationType.DisputeOpened, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid?>()),
            Times.Once);
    }

    [Fact]
    public async Task RespondToDisputeAsync_DisputeNotFound_ReturnsNull()
    {
        var request = new RespondToDisputeRequest
        {
            Response = "Test response to dispute"
        };

        var result = await _disputeService.RespondToDisputeAsync(Guid.NewGuid(), request, Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task RespondToDisputeAsync_UserNotRespondent_ReturnsNull()
    {
        var (complainer, respondent, agreement) = await SeedAgreementAsync();

        var dispute = new Dispute
        {
            Id = Guid.NewGuid(),
            AgreementId = agreement.Id,
            OpenedById = complainer.Id,
            RespondentId = respondent.Id,
            ReasonCode = DisputeReasonCode.WorkNotDelivered,
            Description = "Test dispute",
            Status = DisputeStatus.AwaitingResponse,
            ResponseDeadline = DateTime.UtcNow.AddHours(72),
            Agreement = agreement,
            OpenedBy = complainer,
            Respondent = respondent
        };
        _context.Disputes.Add(dispute);
        await _context.SaveChangesAsync();

        var request = new RespondToDisputeRequest { Response = "Test response" };

        var result = await _disputeService.RespondToDisputeAsync(dispute.Id, request, complainer.Id);

        Assert.Null(result);
    }

    [Fact]
    public async Task RespondToDisputeAsync_DisputeNotAwaitingResponse_ReturnsNull()
    {
        var (complainer, respondent, agreement) = await SeedAgreementAsync();

        var dispute = new Dispute
        {
            Id = Guid.NewGuid(),
            AgreementId = agreement.Id,
            OpenedById = complainer.Id,
            RespondentId = respondent.Id,
            ReasonCode = DisputeReasonCode.WorkNotDelivered,
            Description = "Test dispute",
            Status = DisputeStatus.Resolved,
            ResponseDeadline = DateTime.UtcNow.AddHours(72),
            Agreement = agreement,
            OpenedBy = complainer,
            Respondent = respondent
        };
        _context.Disputes.Add(dispute);
        await _context.SaveChangesAsync();

        var request = new RespondToDisputeRequest { Response = "Test response" };

        var result = await _disputeService.RespondToDisputeAsync(dispute.Id, request, respondent.Id);

        Assert.Null(result);
    }

    [Fact]
    public async Task RespondToDisputeAsync_Success_UpdatesStatusAndCreatesMessage()
    {
        var (complainer, respondent, agreement) = await SeedAgreementAsync();

        var dispute = new Dispute
        {
            Id = Guid.NewGuid(),
            AgreementId = agreement.Id,
            OpenedById = complainer.Id,
            RespondentId = respondent.Id,
            ReasonCode = DisputeReasonCode.WorkNotDelivered,
            Description = "Test dispute",
            Status = DisputeStatus.AwaitingResponse,
            Score = 50,
            ResponseDeadline = DateTime.UtcNow.AddHours(72),
            Agreement = agreement,
            OpenedBy = complainer,
            Respondent = respondent
        };
        _context.Disputes.Add(dispute);
        await _context.SaveChangesAsync();

        var request = new RespondToDisputeRequest
        {
            Response = "My response to this dispute",
            Evidence = new List<EvidenceRequest>
            {
                new() { Link = "http://evidence.com/response", Description = "Response evidence" }
            }
        };

        var result = await _disputeService.RespondToDisputeAsync(dispute.Id, request, respondent.Id);

        Assert.NotNull(result);
        Assert.NotNull(result!.ResponseReceivedAt);

        var storedDispute = await _context.Disputes.FindAsync(dispute.Id);
        Assert.NotNull(storedDispute!.ResponseReceivedAt);

        var messages = await _context.DisputeMessages.Where(m => m.DisputeId == dispute.Id).ToListAsync();
        Assert.Single(messages);
        Assert.Equal(request.Response, messages.First().Body);

        _notificationServiceMock.Verify(
            n => n.CreateAsync(complainer.Id, NotificationType.DisputeResponse, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid?>()),
            Times.Once);
    }

    [Fact]
    public async Task RespondToDisputeAsync_HighScore_AutoResolvesForRespondent()
    {
        var (complainer, respondent, agreement) = await SeedAgreementAsync();

        var dispute = new Dispute
        {
            Id = Guid.NewGuid(),
            AgreementId = agreement.Id,
            OpenedById = complainer.Id,
            RespondentId = respondent.Id,
            ReasonCode = DisputeReasonCode.WorkNotDelivered,
            Description = "Test dispute",
            Status = DisputeStatus.AwaitingResponse,
            Score = 75,
            ResponseDeadline = DateTime.UtcNow.AddHours(72),
            Agreement = agreement,
            OpenedBy = complainer,
            Respondent = respondent
        };
        _context.Disputes.Add(dispute);
        await _context.SaveChangesAsync();

        var request = new RespondToDisputeRequest { Response = "My response" };

        var result = await _disputeService.RespondToDisputeAsync(dispute.Id, request, respondent.Id);

        Assert.NotNull(result);
        Assert.Equal(DisputeStatus.Resolved, result!.Status);
        Assert.Equal(DisputeResolution.FavorsRespondent, result.Resolution);

        var penalties = await _context.Penalties.Where(p => p.DisputeId == dispute.Id).ToListAsync();
        Assert.Single(penalties);
        Assert.Equal(complainer.Id, penalties.First().UserId);
    }

    [Fact]
    public async Task RespondToDisputeAsync_LowScore_AutoResolvesForComplainer()
    {
        var (complainer, respondent, agreement) = await SeedAgreementAsync();

        var dispute = new Dispute
        {
            Id = Guid.NewGuid(),
            AgreementId = agreement.Id,
            OpenedById = complainer.Id,
            RespondentId = respondent.Id,
            ReasonCode = DisputeReasonCode.WorkNotDelivered,
            Description = "Test dispute",
            Status = DisputeStatus.AwaitingResponse,
            Score = 30,
            ResponseDeadline = DateTime.UtcNow.AddHours(72),
            Agreement = agreement,
            OpenedBy = complainer,
            Respondent = respondent
        };
        _context.Disputes.Add(dispute);
        await _context.SaveChangesAsync();

        var request = new RespondToDisputeRequest { Response = "My response" };

        var result = await _disputeService.RespondToDisputeAsync(dispute.Id, request, respondent.Id);

        Assert.NotNull(result);
        Assert.Equal(DisputeStatus.Resolved, result!.Status);
        Assert.Equal(DisputeResolution.FavorsComplainer, result.Resolution);

        var penalties = await _context.Penalties.Where(p => p.DisputeId == dispute.Id).ToListAsync();
        Assert.Single(penalties);
        Assert.Equal(respondent.Id, penalties.First().UserId);
    }

    [Fact]
    public async Task RespondToDisputeAsync_MidScore_EscalatesToModerator()
    {
        var (complainer, respondent, agreement) = await SeedAgreementAsync();

        var dispute = new Dispute
        {
            Id = Guid.NewGuid(),
            AgreementId = agreement.Id,
            OpenedById = complainer.Id,
            RespondentId = respondent.Id,
            ReasonCode = DisputeReasonCode.WorkNotDelivered,
            Description = "Test dispute",
            Status = DisputeStatus.AwaitingResponse,
            Score = 50,
            ResponseDeadline = DateTime.UtcNow.AddHours(72),
            Agreement = agreement,
            OpenedBy = complainer,
            Respondent = respondent
        };
        _context.Disputes.Add(dispute);
        await _context.SaveChangesAsync();

        var request = new RespondToDisputeRequest { Response = "My response" };

        var result = await _disputeService.RespondToDisputeAsync(dispute.Id, request, respondent.Id);

        Assert.NotNull(result);
        Assert.Equal(DisputeStatus.EscalatedToModerator, result!.Status);
        Assert.True(result.IsEscalated);

        _notificationServiceMock.Verify(
            n => n.CreateAsync(It.IsAny<Guid>(), NotificationType.DisputeEscalated, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid?>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task AddEvidenceAsync_DisputeNotFound_ReturnsNull()
    {
        var request = new EvidenceRequest { Link = "http://test.com", Description = "Test" };

        var result = await _disputeService.AddEvidenceAsync(Guid.NewGuid(), request, Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task AddEvidenceAsync_UserNotPartOfDispute_ReturnsNull()
    {
        var (complainer, respondent, agreement) = await SeedAgreementAsync();

        var dispute = new Dispute
        {
            Id = Guid.NewGuid(),
            AgreementId = agreement.Id,
            OpenedById = complainer.Id,
            RespondentId = respondent.Id,
            ReasonCode = DisputeReasonCode.WorkNotDelivered,
            Description = "Test dispute",
            Status = DisputeStatus.AwaitingResponse,
            Agreement = agreement,
            OpenedBy = complainer,
            Respondent = respondent
        };
        _context.Disputes.Add(dispute);
        await _context.SaveChangesAsync();

        var outsider = Guid.NewGuid();
        var request = new EvidenceRequest { Link = "http://test.com", Description = "Test" };

        var result = await _disputeService.AddEvidenceAsync(dispute.Id, request, outsider);

        Assert.Null(result);
    }

    [Fact]
    public async Task AddEvidenceAsync_Success_AddsEvidence()
    {
        var (complainer, respondent, agreement) = await SeedAgreementAsync();

        var dispute = new Dispute
        {
            Id = Guid.NewGuid(),
            AgreementId = agreement.Id,
            OpenedById = complainer.Id,
            RespondentId = respondent.Id,
            ReasonCode = DisputeReasonCode.WorkNotDelivered,
            Description = "Test dispute",
            Status = DisputeStatus.AwaitingResponse,
            Agreement = agreement,
            OpenedBy = complainer,
            Respondent = respondent
        };
        _context.Disputes.Add(dispute);
        await _context.SaveChangesAsync();

        var request = new EvidenceRequest { Link = "http://newevidence.com", Description = "New evidence" };

        var result = await _disputeService.AddEvidenceAsync(dispute.Id, request, complainer.Id);

        Assert.NotNull(result);

        var evidence = await _context.Set<DisputeEvidence>().Where(e => e.DisputeId == dispute.Id).ToListAsync();
        Assert.Single(evidence);
        Assert.Equal(request.Link, evidence.First().Link);
    }

    [Fact]
    public async Task GetDisputeByIdAsync_NotFound_ReturnsNull()
    {
        var result = await _disputeService.GetDisputeByIdAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task GetDisputeByIdAsync_UserNotPartOfDispute_ReturnsNull()
    {
        var (complainer, respondent, agreement) = await SeedAgreementAsync();

        var dispute = new Dispute
        {
            Id = Guid.NewGuid(),
            AgreementId = agreement.Id,
            OpenedById = complainer.Id,
            RespondentId = respondent.Id,
            ReasonCode = DisputeReasonCode.WorkNotDelivered,
            Description = "Test dispute",
            Status = DisputeStatus.AwaitingResponse,
            Agreement = agreement,
            OpenedBy = complainer,
            Respondent = respondent
        };
        _context.Disputes.Add(dispute);
        await _context.SaveChangesAsync();

        var outsider = Guid.NewGuid();

        var result = await _disputeService.GetDisputeByIdAsync(dispute.Id, outsider);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetDisputeByIdAsync_AsParticipant_ReturnsDispute()
    {
        var (complainer, respondent, agreement) = await SeedAgreementAsync();

        var dispute = new Dispute
        {
            Id = Guid.NewGuid(),
            AgreementId = agreement.Id,
            OpenedById = complainer.Id,
            RespondentId = respondent.Id,
            ReasonCode = DisputeReasonCode.WorkNotDelivered,
            Description = "Test dispute",
            Status = DisputeStatus.AwaitingResponse,
            Score = 50,
            ResponseDeadline = DateTime.UtcNow.AddHours(72),
            Agreement = agreement,
            OpenedBy = complainer,
            Respondent = respondent
        };
        _context.Disputes.Add(dispute);
        await _context.SaveChangesAsync();

        var result = await _disputeService.GetDisputeByIdAsync(dispute.Id, complainer.Id);

        Assert.NotNull(result);
        Assert.Equal(dispute.Id, result!.Id);
        Assert.Equal(complainer.Name, result.Complainer.Name);
    }

    [Fact]
    public async Task GetDisputeByIdAsync_AsModerator_ReturnsDispute()
    {
        var (complainer, respondent, agreement) = await SeedAgreementAsync();

        var moderator = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = "moderator",
            IsModerator = true
        };
        _context.Users.Add(moderator);

        var dispute = new Dispute
        {
            Id = Guid.NewGuid(),
            AgreementId = agreement.Id,
            OpenedById = complainer.Id,
            RespondentId = respondent.Id,
            ReasonCode = DisputeReasonCode.WorkNotDelivered,
            Description = "Test dispute",
            Status = DisputeStatus.EscalatedToModerator,
            Agreement = agreement,
            OpenedBy = complainer,
            Respondent = respondent
        };
        _context.Disputes.Add(dispute);
        await _context.SaveChangesAsync();

        var result = await _disputeService.GetDisputeByIdAsync(dispute.Id, moderator.Id);

        Assert.NotNull(result);
        Assert.Equal(dispute.Id, result!.Id);
    }

    [Fact]
    public async Task GetMyDisputesAsync_ReturnsUserDisputes()
    {
        var (complainer, respondent, agreement) = await SeedAgreementAsync();

        var dispute1 = new Dispute
        {
            Id = Guid.NewGuid(),
            AgreementId = agreement.Id,
            OpenedById = complainer.Id,
            RespondentId = respondent.Id,
            ReasonCode = DisputeReasonCode.WorkNotDelivered,
            Description = "Dispute 1",
            Status = DisputeStatus.AwaitingResponse,
            OpenedBy = complainer,
            Respondent = respondent
        };
        _context.Disputes.Add(dispute1);
        await _context.SaveChangesAsync();

        var result = await _disputeService.GetMyDisputesAsync(complainer.Id);

        Assert.Single(result);
        Assert.Equal(dispute1.Id, result.First().Id);
    }

    [Fact]
    public async Task GetDisputesForModerationAsync_NotModerator_ReturnsEmpty()
    {
        var result = await _disputeService.GetDisputesForModerationAsync(Guid.NewGuid());

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetDisputesForModerationAsync_AsModerator_ReturnsEscalatedDisputes()
    {
        var (complainer, respondent, agreement) = await SeedAgreementAsync();

        var moderator = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = "moderator",
            IsModerator = true
        };
        _context.Users.Add(moderator);

        var escalatedDispute = new Dispute
        {
            Id = Guid.NewGuid(),
            AgreementId = agreement.Id,
            OpenedById = complainer.Id,
            RespondentId = respondent.Id,
            ReasonCode = DisputeReasonCode.WorkNotDelivered,
            Description = "Escalated dispute",
            Status = DisputeStatus.EscalatedToModerator,
            EscalatedAt = DateTime.UtcNow,
            OpenedBy = complainer,
            Respondent = respondent
        };

        var nonEscalatedDispute = new Dispute
        {
            Id = Guid.NewGuid(),
            AgreementId = agreement.Id,
            OpenedById = complainer.Id,
            RespondentId = respondent.Id,
            ReasonCode = DisputeReasonCode.WorkNotDelivered,
            Description = "Non-escalated dispute",
            Status = DisputeStatus.AwaitingResponse,
            OpenedBy = complainer,
            Respondent = respondent
        };

        _context.Disputes.AddRange(escalatedDispute, nonEscalatedDispute);
        await _context.SaveChangesAsync();

        var result = await _disputeService.GetDisputesForModerationAsync(moderator.Id);

        Assert.Single(result);
        Assert.Equal(escalatedDispute.Id, result.First().Id);
    }

    [Fact]
    public async Task MakeModeratorDecisionAsync_NotModerator_ReturnsNull()
    {
        var request = new ModeratorDecisionRequest
        {
            Resolution = DisputeResolution.FavorsComplainer,
            Notes = "Moderator notes"
        };

        var result = await _disputeService.MakeModeratorDecisionAsync(Guid.NewGuid(), request, Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task MakeModeratorDecisionAsync_DisputeNotEscalated_ReturnsNull()
    {
        var (complainer, respondent, agreement) = await SeedAgreementAsync();

        var moderator = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = "moderator",
            IsModerator = true
        };
        _context.Users.Add(moderator);

        var dispute = new Dispute
        {
            Id = Guid.NewGuid(),
            AgreementId = agreement.Id,
            OpenedById = complainer.Id,
            RespondentId = respondent.Id,
            ReasonCode = DisputeReasonCode.WorkNotDelivered,
            Description = "Test dispute",
            Status = DisputeStatus.AwaitingResponse,
            Agreement = agreement,
            OpenedBy = complainer,
            Respondent = respondent
        };
        _context.Disputes.Add(dispute);
        await _context.SaveChangesAsync();

        var request = new ModeratorDecisionRequest
        {
            Resolution = DisputeResolution.FavorsComplainer,
            Notes = "Moderator notes"
        };

        var result = await _disputeService.MakeModeratorDecisionAsync(dispute.Id, request, moderator.Id);

        Assert.Null(result);
    }

    [Fact]
    public async Task MakeModeratorDecisionAsync_FavorsComplainer_CreatesPenaltyForRespondent()
    {
        var (complainer, respondent, agreement) = await SeedAgreementAsync();

        var moderator = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = "moderator",
            IsModerator = true
        };
        _context.Users.Add(moderator);

        var dispute = new Dispute
        {
            Id = Guid.NewGuid(),
            AgreementId = agreement.Id,
            OpenedById = complainer.Id,
            RespondentId = respondent.Id,
            ReasonCode = DisputeReasonCode.WorkNotDelivered,
            Description = "Test dispute",
            Status = DisputeStatus.EscalatedToModerator,
            Score = 50,
            Agreement = agreement,
            OpenedBy = complainer,
            Respondent = respondent
        };
        _context.Disputes.Add(dispute);
        await _context.SaveChangesAsync();

        var request = new ModeratorDecisionRequest
        {
            Resolution = DisputeResolution.FavorsComplainer,
            Notes = "Respondent failed to deliver"
        };

        var result = await _disputeService.MakeModeratorDecisionAsync(dispute.Id, request, moderator.Id);

        Assert.NotNull(result);
        Assert.Equal(DisputeStatus.Resolved, result!.Status);
        Assert.Equal(DisputeResolution.FavorsComplainer, result.Resolution);

        var penalties = await _context.Penalties.Where(p => p.DisputeId == dispute.Id).ToListAsync();
        Assert.Single(penalties);
        Assert.Equal(respondent.Id, penalties.First().UserId);

        _notificationServiceMock.Verify(
            n => n.CreateAsync(It.IsAny<Guid>(), NotificationType.DisputeResolved, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid?>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task MakeModeratorDecisionAsync_FavorsRespondent_CreatesPenaltyForComplainer()
    {
        var (complainer, respondent, agreement) = await SeedAgreementAsync();

        var moderator = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = "moderator",
            IsModerator = true
        };
        _context.Users.Add(moderator);

        var dispute = new Dispute
        {
            Id = Guid.NewGuid(),
            AgreementId = agreement.Id,
            OpenedById = complainer.Id,
            RespondentId = respondent.Id,
            ReasonCode = DisputeReasonCode.WorkNotDelivered,
            Description = "Test dispute",
            Status = DisputeStatus.EscalatedToModerator,
            Score = 50,
            Agreement = agreement,
            OpenedBy = complainer,
            Respondent = respondent
        };
        _context.Disputes.Add(dispute);
        await _context.SaveChangesAsync();

        var request = new ModeratorDecisionRequest
        {
            Resolution = DisputeResolution.FavorsRespondent,
            Notes = "Complainer's claim was invalid"
        };

        var result = await _disputeService.MakeModeratorDecisionAsync(dispute.Id, request, moderator.Id);

        Assert.NotNull(result);
        Assert.Equal(DisputeStatus.Resolved, result!.Status);
        Assert.Equal(DisputeResolution.FavorsRespondent, result.Resolution);

        var penalties = await _context.Penalties.Where(p => p.DisputeId == dispute.Id).ToListAsync();
        Assert.Single(penalties);
        Assert.Equal(complainer.Id, penalties.First().UserId);
    }

    [Fact]
    public async Task ProcessExpiredDisputesAsync_MarksExpiredAndCreatesPenalty()
    {
        var (complainer, respondent, agreement) = await SeedAgreementAsync();

        var expiredDispute = new Dispute
        {
            Id = Guid.NewGuid(),
            AgreementId = agreement.Id,
            OpenedById = complainer.Id,
            RespondentId = respondent.Id,
            ReasonCode = DisputeReasonCode.WorkNotDelivered,
            Description = "Expired dispute",
            Status = DisputeStatus.AwaitingResponse,
            ResponseDeadline = DateTime.UtcNow.AddHours(-1),
            Agreement = agreement
        };

        var activeDispute = new Dispute
        {
            Id = Guid.NewGuid(),
            AgreementId = agreement.Id,
            OpenedById = complainer.Id,
            RespondentId = respondent.Id,
            ReasonCode = DisputeReasonCode.WorkNotDelivered,
            Description = "Active dispute",
            Status = DisputeStatus.AwaitingResponse,
            ResponseDeadline = DateTime.UtcNow.AddHours(48),
            Agreement = agreement
        };

        _context.Disputes.AddRange(expiredDispute, activeDispute);
        await _context.SaveChangesAsync();

        await _disputeService.ProcessExpiredDisputesAsync();

        var storedExpired = await _context.Disputes.FindAsync(expiredDispute.Id);
        Assert.Equal(DisputeStatus.Resolved, storedExpired!.Status);
        Assert.Equal(DisputeResolution.FavorsComplainer, storedExpired.Resolution);

        var storedActive = await _context.Disputes.FindAsync(activeDispute.Id);
        Assert.Equal(DisputeStatus.AwaitingResponse, storedActive!.Status);

        var penalties = await _context.Penalties.Where(p => p.DisputeId == expiredDispute.Id).ToListAsync();
        Assert.Single(penalties);
        Assert.Equal(respondent.Id, penalties.First().UserId);
        Assert.Equal(PenaltyReason.NoDisputeResponse, penalties.First().Reason);
    }
}
