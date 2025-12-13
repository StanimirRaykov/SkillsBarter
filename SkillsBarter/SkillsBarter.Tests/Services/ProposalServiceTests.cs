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

public class ProposalServiceTests
{
    private readonly Mock<IAgreementService> _agreementServiceMock = new();
    private readonly Mock<INotificationService> _notificationServiceMock = new();
    private readonly Mock<ILogger<ProposalService>> _loggerMock = new();
    private ApplicationDbContext _context = null!;
    private ProposalService _proposalService = null!;

    public ProposalServiceTests()
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
        _proposalService = new ProposalService(
            _context,
            _agreementServiceMock.Object,
            _notificationServiceMock.Object,
            _loggerMock.Object
        );
    }

    private async Task<(ApplicationUser proposer, ApplicationUser offerOwner, Offer offer, Skill skill)> SeedOfferAsync(
        OfferStatusCode status = OfferStatusCode.Active)
    {
        var proposer = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Name = "Proposer",
            UserName = "proposer",
            Email = "proposer@example.com"
        };

        var offerOwner = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Name = "Offer Owner",
            UserName = "offerowner",
            Email = "owner@example.com"
        };

        var category = new SkillCategory { Code = "TECH", Label = "Technology" };
        var skill = new Skill { Id = 1, Name = "Programming", CategoryCode = category.Code, Category = category };

        var offerStatuses = new[]
        {
            new OfferStatus { Code = OfferStatusCode.Active, Label = "Active" },
            new OfferStatus { Code = OfferStatusCode.UnderAgreement, Label = "Under Agreement" },
            new OfferStatus { Code = OfferStatusCode.Completed, Label = "Completed" }
        };

        _context.Users.AddRange(proposer, offerOwner);
        _context.SkillCategories.Add(category);
        _context.Skills.Add(skill);
        _context.OfferStatuses.AddRange(offerStatuses);

        var offer = new Offer
        {
            Id = Guid.NewGuid(),
            UserId = offerOwner.Id,
            SkillId = skill.Id,
            Title = "Test Offer",
            Description = "Test Description",
            StatusCode = status,
            User = offerOwner,
            Skill = skill
        };

        _context.Offers.Add(offer);
        await _context.SaveChangesAsync();

        return (proposer, offerOwner, offer, skill);
    }

    [Fact]
    public async Task CreateProposalAsync_OfferNotFound_ReturnsNull()
    {
        var request = new CreateProposalRequest
        {
            OfferId = Guid.NewGuid(),
            Terms = "Test terms content",
            ProposerOffer = "Test proposer offer",
            Deadline = DateTime.UtcNow.AddDays(7)
        };

        var result = await _proposalService.CreateProposalAsync(request, Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task CreateProposalAsync_OfferNotActive_ReturnsNull()
    {
        var (proposer, _, offer, _) = await SeedOfferAsync(OfferStatusCode.Completed);

        var request = new CreateProposalRequest
        {
            OfferId = offer.Id,
            Terms = "Test terms content",
            ProposerOffer = "Test proposer offer",
            Deadline = DateTime.UtcNow.AddDays(7)
        };

        var result = await _proposalService.CreateProposalAsync(request, proposer.Id);

        Assert.Null(result);
    }

    [Fact]
    public async Task CreateProposalAsync_ProposerIsOfferOwner_ReturnsNull()
    {
        var (_, offerOwner, offer, _) = await SeedOfferAsync();

        var request = new CreateProposalRequest
        {
            OfferId = offer.Id,
            Terms = "Test terms content",
            ProposerOffer = "Test proposer offer",
            Deadline = DateTime.UtcNow.AddDays(7)
        };

        var result = await _proposalService.CreateProposalAsync(request, offerOwner.Id);

        Assert.Null(result);
    }

    [Fact]
    public async Task CreateProposalAsync_DeadlineInPast_ReturnsNull()
    {
        var (proposer, _, offer, _) = await SeedOfferAsync();

        var request = new CreateProposalRequest
        {
            OfferId = offer.Id,
            Terms = "Test terms content",
            ProposerOffer = "Test proposer offer",
            Deadline = DateTime.UtcNow.AddDays(-1)
        };

        var result = await _proposalService.CreateProposalAsync(request, proposer.Id);

        Assert.Null(result);
    }

    [Fact]
    public async Task CreateProposalAsync_Success_CreatesProposalAndHistory()
    {
        var (proposer, offerOwner, offer, _) = await SeedOfferAsync();

        var request = new CreateProposalRequest
        {
            OfferId = offer.Id,
            Terms = "Test terms content",
            ProposerOffer = "Test proposer offer",
            Deadline = DateTime.UtcNow.AddDays(7),
            Message = "Initial message"
        };

        var result = await _proposalService.CreateProposalAsync(request, proposer.Id);

        Assert.NotNull(result);
        Assert.Equal(proposer.Id, result!.ProposerId);
        Assert.Equal(offerOwner.Id, result.OfferOwnerId);
        Assert.Equal(ProposalStatus.PendingOfferOwnerReview, result.Status);
        Assert.Equal(offerOwner.Id, result.PendingResponseFromUserId);

        var storedProposal = await _context.Proposals.FirstOrDefaultAsync(p => p.Id == result.Id);
        Assert.NotNull(storedProposal);

        var history = await _context.ProposalHistories.Where(h => h.ProposalId == result.Id).ToListAsync();
        Assert.Single(history);
        Assert.Equal(ProposalAction.Created, history.First().Action);

        _notificationServiceMock.Verify(
            n => n.CreateAsync(offerOwner.Id, NotificationType.ProposalReceived, It.IsAny<string>(), It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateProposalAsync_ExistingPendingProposal_ReturnsNull()
    {
        var (proposer, offerOwner, offer, _) = await SeedOfferAsync();

        var existingProposal = new Proposal
        {
            Id = Guid.NewGuid(),
            OfferId = offer.Id,
            ProposerId = proposer.Id,
            OfferOwnerId = offerOwner.Id,
            Terms = "Existing terms",
            ProposerOffer = "Existing offer",
            Deadline = DateTime.UtcNow.AddDays(5),
            Status = ProposalStatus.PendingOfferOwnerReview,
            PendingResponseFromUserId = offerOwner.Id
        };
        _context.Proposals.Add(existingProposal);
        await _context.SaveChangesAsync();

        var request = new CreateProposalRequest
        {
            OfferId = offer.Id,
            Terms = "New terms",
            ProposerOffer = "New offer",
            Deadline = DateTime.UtcNow.AddDays(7)
        };

        var result = await _proposalService.CreateProposalAsync(request, proposer.Id);

        Assert.Null(result);
    }

    [Fact]
    public async Task RespondToProposalAsync_ProposalNotFound_ReturnsNull()
    {
        var request = new RespondToProposalRequest { Action = ProposalResponseAction.Accept };

        var result = await _proposalService.RespondToProposalAsync(Guid.NewGuid(), request, Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task RespondToProposalAsync_UserNotParticipant_ReturnsNull()
    {
        var (proposer, offerOwner, offer, _) = await SeedOfferAsync();

        var proposal = new Proposal
        {
            Id = Guid.NewGuid(),
            OfferId = offer.Id,
            ProposerId = proposer.Id,
            OfferOwnerId = offerOwner.Id,
            Terms = "Terms",
            ProposerOffer = "Offer",
            Deadline = DateTime.UtcNow.AddDays(5),
            Status = ProposalStatus.PendingOfferOwnerReview,
            PendingResponseFromUserId = offerOwner.Id,
            Offer = offer,
            Proposer = proposer,
            OfferOwner = offerOwner
        };
        _context.Proposals.Add(proposal);
        await _context.SaveChangesAsync();

        var outsiderId = Guid.NewGuid();
        var request = new RespondToProposalRequest { Action = ProposalResponseAction.Accept };

        var result = await _proposalService.RespondToProposalAsync(proposal.Id, request, outsiderId);

        Assert.Null(result);
    }

    [Fact]
    public async Task RespondToProposalAsync_NotUsersTurn_ReturnsNull()
    {
        var (proposer, offerOwner, offer, _) = await SeedOfferAsync();

        var proposal = new Proposal
        {
            Id = Guid.NewGuid(),
            OfferId = offer.Id,
            ProposerId = proposer.Id,
            OfferOwnerId = offerOwner.Id,
            Terms = "Terms",
            ProposerOffer = "Offer",
            Deadline = DateTime.UtcNow.AddDays(5),
            Status = ProposalStatus.PendingOfferOwnerReview,
            PendingResponseFromUserId = offerOwner.Id,
            Offer = offer,
            Proposer = proposer,
            OfferOwner = offerOwner
        };
        _context.Proposals.Add(proposal);
        await _context.SaveChangesAsync();

        var request = new RespondToProposalRequest { Action = ProposalResponseAction.Accept };

        var result = await _proposalService.RespondToProposalAsync(proposal.Id, request, proposer.Id);

        Assert.Null(result);
    }

    [Fact]
    public async Task RespondToProposalAsync_Accept_CreatesAgreementAndUpdatesStatus()
    {
        var (proposer, offerOwner, offer, _) = await SeedOfferAsync();

        var proposal = new Proposal
        {
            Id = Guid.NewGuid(),
            OfferId = offer.Id,
            ProposerId = proposer.Id,
            OfferOwnerId = offerOwner.Id,
            Terms = "Terms",
            ProposerOffer = "Offer",
            Deadline = DateTime.UtcNow.AddDays(5),
            Status = ProposalStatus.PendingOfferOwnerReview,
            PendingResponseFromUserId = offerOwner.Id,
            Offer = offer,
            Proposer = proposer,
            OfferOwner = offerOwner
        };
        _context.Proposals.Add(proposal);
        await _context.SaveChangesAsync();

        var agreementId = Guid.NewGuid();
        _agreementServiceMock.Setup(a => a.CreateAgreementAsync(
                offer.Id, proposer.Id, offerOwner.Id, proposal.Terms))
            .ReturnsAsync(new AgreementResponse { Id = agreementId });

        var request = new RespondToProposalRequest { Action = ProposalResponseAction.Accept };

        var result = await _proposalService.RespondToProposalAsync(proposal.Id, request, offerOwner.Id);

        Assert.NotNull(result);
        Assert.Equal(ProposalStatus.Accepted, result!.Status);
        Assert.Equal(agreementId, result.AgreementId);
        Assert.Null(result.PendingResponseFromUserId);

        _notificationServiceMock.Verify(
            n => n.CreateAsync(proposer.Id, NotificationType.ProposalAccepted, It.IsAny<string>(), It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public async Task RespondToProposalAsync_Decline_UpdatesStatusAndReason()
    {
        var (proposer, offerOwner, offer, _) = await SeedOfferAsync();

        var proposal = new Proposal
        {
            Id = Guid.NewGuid(),
            OfferId = offer.Id,
            ProposerId = proposer.Id,
            OfferOwnerId = offerOwner.Id,
            Terms = "Terms",
            ProposerOffer = "Offer",
            Deadline = DateTime.UtcNow.AddDays(5),
            Status = ProposalStatus.PendingOfferOwnerReview,
            PendingResponseFromUserId = offerOwner.Id,
            Offer = offer,
            Proposer = proposer,
            OfferOwner = offerOwner
        };
        _context.Proposals.Add(proposal);
        await _context.SaveChangesAsync();

        var request = new RespondToProposalRequest
        {
            Action = ProposalResponseAction.Decline,
            Message = "Not interested"
        };

        var result = await _proposalService.RespondToProposalAsync(proposal.Id, request, offerOwner.Id);

        Assert.NotNull(result);
        Assert.Equal(ProposalStatus.Declined, result!.Status);

        var storedProposal = await _context.Proposals.FindAsync(proposal.Id);
        Assert.Equal("Not interested", storedProposal!.DeclineReason);

        _notificationServiceMock.Verify(
            n => n.CreateAsync(proposer.Id, NotificationType.ProposalDeclined, It.IsAny<string>(), It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public async Task RespondToProposalAsync_Modify_UpdatesTermsAndSwitchesTurn()
    {
        var (proposer, offerOwner, offer, _) = await SeedOfferAsync();

        var proposal = new Proposal
        {
            Id = Guid.NewGuid(),
            OfferId = offer.Id,
            ProposerId = proposer.Id,
            OfferOwnerId = offerOwner.Id,
            Terms = "Original terms",
            ProposerOffer = "Original offer",
            Deadline = DateTime.UtcNow.AddDays(5),
            Status = ProposalStatus.PendingOfferOwnerReview,
            PendingResponseFromUserId = offerOwner.Id,
            ModificationCount = 0,
            Offer = offer,
            Proposer = proposer,
            OfferOwner = offerOwner
        };
        _context.Proposals.Add(proposal);
        await _context.SaveChangesAsync();

        var request = new RespondToProposalRequest
        {
            Action = ProposalResponseAction.Modify,
            Terms = "Modified terms",
            ProposerOffer = "Modified offer"
        };

        var result = await _proposalService.RespondToProposalAsync(proposal.Id, request, offerOwner.Id);

        Assert.NotNull(result);
        Assert.Equal("Modified terms", result!.Terms);
        Assert.Equal("Modified offer", result.ProposerOffer);
        Assert.Equal(1, result.ModificationCount);
        Assert.Equal(proposer.Id, result.PendingResponseFromUserId);

        _notificationServiceMock.Verify(
            n => n.CreateAsync(proposer.Id, NotificationType.ProposalModified, It.IsAny<string>(), It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public async Task RespondToProposalAsync_ModifyWithoutTerms_ReturnsNull()
    {
        var (proposer, offerOwner, offer, _) = await SeedOfferAsync();

        var proposal = new Proposal
        {
            Id = Guid.NewGuid(),
            OfferId = offer.Id,
            ProposerId = proposer.Id,
            OfferOwnerId = offerOwner.Id,
            Terms = "Original terms",
            ProposerOffer = "Original offer",
            Deadline = DateTime.UtcNow.AddDays(5),
            Status = ProposalStatus.PendingOfferOwnerReview,
            PendingResponseFromUserId = offerOwner.Id,
            Offer = offer,
            Proposer = proposer,
            OfferOwner = offerOwner
        };
        _context.Proposals.Add(proposal);
        await _context.SaveChangesAsync();

        var request = new RespondToProposalRequest
        {
            Action = ProposalResponseAction.Modify,
            Terms = null,
            ProposerOffer = null
        };

        var result = await _proposalService.RespondToProposalAsync(proposal.Id, request, offerOwner.Id);

        Assert.Null(result);
    }

    [Fact]
    public async Task WithdrawProposalAsync_ProposalNotFound_ReturnsFalse()
    {
        var result = await _proposalService.WithdrawProposalAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.False(result);
    }

    [Fact]
    public async Task WithdrawProposalAsync_UserNotProposer_ReturnsFalse()
    {
        var (proposer, offerOwner, offer, _) = await SeedOfferAsync();

        var proposal = new Proposal
        {
            Id = Guid.NewGuid(),
            OfferId = offer.Id,
            ProposerId = proposer.Id,
            OfferOwnerId = offerOwner.Id,
            Terms = "Terms",
            ProposerOffer = "Offer",
            Deadline = DateTime.UtcNow.AddDays(5),
            Status = ProposalStatus.PendingOfferOwnerReview,
            PendingResponseFromUserId = offerOwner.Id,
            Offer = offer
        };
        _context.Proposals.Add(proposal);
        await _context.SaveChangesAsync();

        var result = await _proposalService.WithdrawProposalAsync(proposal.Id, offerOwner.Id);

        Assert.False(result);
    }

    [Fact]
    public async Task WithdrawProposalAsync_Success_UpdatesStatusAndNotifies()
    {
        var (proposer, offerOwner, offer, _) = await SeedOfferAsync();

        var proposal = new Proposal
        {
            Id = Guid.NewGuid(),
            OfferId = offer.Id,
            ProposerId = proposer.Id,
            OfferOwnerId = offerOwner.Id,
            Terms = "Terms",
            ProposerOffer = "Offer",
            Deadline = DateTime.UtcNow.AddDays(5),
            Status = ProposalStatus.PendingOfferOwnerReview,
            PendingResponseFromUserId = offerOwner.Id,
            Offer = offer
        };
        _context.Proposals.Add(proposal);
        await _context.SaveChangesAsync();

        var result = await _proposalService.WithdrawProposalAsync(proposal.Id, proposer.Id);

        Assert.True(result);

        var storedProposal = await _context.Proposals.FindAsync(proposal.Id);
        Assert.Equal(ProposalStatus.Withdrawn, storedProposal!.Status);
        Assert.Null(storedProposal.PendingResponseFromUserId);

        var history = await _context.ProposalHistories.Where(h => h.ProposalId == proposal.Id).ToListAsync();
        Assert.Single(history);
        Assert.Equal(ProposalAction.Withdrawn, history.First().Action);

        _notificationServiceMock.Verify(
            n => n.CreateAsync(offerOwner.Id, NotificationType.ProposalWithdrawn, It.IsAny<string>(), It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public async Task GetProposalByIdAsync_NotFound_ReturnsNull()
    {
        var result = await _proposalService.GetProposalByIdAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task GetProposalByIdAsync_Found_ReturnsProposal()
    {
        var (proposer, offerOwner, offer, _) = await SeedOfferAsync();

        var proposal = new Proposal
        {
            Id = Guid.NewGuid(),
            OfferId = offer.Id,
            ProposerId = proposer.Id,
            OfferOwnerId = offerOwner.Id,
            Terms = "Terms",
            ProposerOffer = "Offer",
            Deadline = DateTime.UtcNow.AddDays(5),
            Status = ProposalStatus.PendingOfferOwnerReview,
            PendingResponseFromUserId = offerOwner.Id,
            Offer = offer,
            Proposer = proposer,
            OfferOwner = offerOwner
        };
        _context.Proposals.Add(proposal);
        await _context.SaveChangesAsync();

        var result = await _proposalService.GetProposalByIdAsync(proposal.Id);

        Assert.NotNull(result);
        Assert.Equal(proposal.Id, result!.Id);
        Assert.Equal(proposer.Name, result.ProposerName);
    }

    [Fact]
    public async Task GetProposalDetailByIdAsync_NotFound_ReturnsNull()
    {
        var result = await _proposalService.GetProposalDetailByIdAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task GetProposalDetailByIdAsync_Found_ReturnsMappedDetail()
    {
        var (proposer, offerOwner, offer, skill) = await SeedOfferAsync();

        var proposal = new Proposal
        {
            Id = Guid.NewGuid(),
            OfferId = offer.Id,
            ProposerId = proposer.Id,
            OfferOwnerId = offerOwner.Id,
            Terms = "Terms",
            ProposerOffer = "Offer",
            Deadline = DateTime.UtcNow.AddDays(5),
            Status = ProposalStatus.PendingOfferOwnerReview,
            PendingResponseFromUserId = offerOwner.Id,
            Offer = offer,
            Proposer = proposer,
            OfferOwner = offerOwner,
            History = new List<ProposalHistory>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    ActorId = proposer.Id,
                    Actor = proposer,
                    Action = ProposalAction.Created,
                    Terms = "Terms",
                    ProposerOffer = "Offer",
                    Deadline = DateTime.UtcNow.AddDays(5),
                    CreatedAt = DateTime.UtcNow
                }
            }
        };
        offer.Skill = skill;
        _context.Proposals.Add(proposal);
        await _context.SaveChangesAsync();

        var result = await _proposalService.GetProposalDetailByIdAsync(proposal.Id);

        Assert.NotNull(result);
        Assert.Equal(proposal.Id, result!.Id);
        Assert.Equal(proposer.Name, result.Proposer.Name);
        Assert.Equal(offerOwner.Name, result.OfferOwner.Name);
        Assert.Equal(skill.Name, result.Offer.SkillName);
        Assert.Single(result.History);
    }

    [Fact]
    public async Task CanUserRespondAsync_ProposalNotFound_ReturnsFalse()
    {
        var result = await _proposalService.CanUserRespondAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.False(result);
    }

    [Fact]
    public async Task CanUserRespondAsync_NotUsersTurn_ReturnsFalse()
    {
        var (proposer, offerOwner, offer, _) = await SeedOfferAsync();

        var proposal = new Proposal
        {
            Id = Guid.NewGuid(),
            OfferId = offer.Id,
            ProposerId = proposer.Id,
            OfferOwnerId = offerOwner.Id,
            Terms = "Terms",
            ProposerOffer = "Offer",
            Deadline = DateTime.UtcNow.AddDays(5),
            Status = ProposalStatus.PendingOfferOwnerReview,
            PendingResponseFromUserId = offerOwner.Id
        };
        _context.Proposals.Add(proposal);
        await _context.SaveChangesAsync();

        var result = await _proposalService.CanUserRespondAsync(proposal.Id, proposer.Id);

        Assert.False(result);
    }

    [Fact]
    public async Task CanUserRespondAsync_IsUsersTurn_ReturnsTrue()
    {
        var (proposer, offerOwner, offer, _) = await SeedOfferAsync();

        var proposal = new Proposal
        {
            Id = Guid.NewGuid(),
            OfferId = offer.Id,
            ProposerId = proposer.Id,
            OfferOwnerId = offerOwner.Id,
            Terms = "Terms",
            ProposerOffer = "Offer",
            Deadline = DateTime.UtcNow.AddDays(5),
            Status = ProposalStatus.PendingOfferOwnerReview,
            PendingResponseFromUserId = offerOwner.Id
        };
        _context.Proposals.Add(proposal);
        await _context.SaveChangesAsync();

        var result = await _proposalService.CanUserRespondAsync(proposal.Id, offerOwner.Id);

        Assert.True(result);
    }

    [Fact]
    public async Task MarkExpiredProposalsAsync_MarksExpiredProposals()
    {
        var (proposer, offerOwner, offer, _) = await SeedOfferAsync();

        var expiredProposal = new Proposal
        {
            Id = Guid.NewGuid(),
            OfferId = offer.Id,
            ProposerId = proposer.Id,
            OfferOwnerId = offerOwner.Id,
            Terms = "Terms",
            ProposerOffer = "Offer",
            Deadline = DateTime.UtcNow.AddDays(-1),
            Status = ProposalStatus.PendingOfferOwnerReview,
            PendingResponseFromUserId = offerOwner.Id
        };

        var activeProposal = new Proposal
        {
            Id = Guid.NewGuid(),
            OfferId = offer.Id,
            ProposerId = proposer.Id,
            OfferOwnerId = offerOwner.Id,
            Terms = "Terms",
            ProposerOffer = "Offer",
            Deadline = DateTime.UtcNow.AddDays(7),
            Status = ProposalStatus.PendingOfferOwnerReview,
            PendingResponseFromUserId = offerOwner.Id
        };

        _context.Proposals.AddRange(expiredProposal, activeProposal);
        await _context.SaveChangesAsync();

        var count = await _proposalService.MarkExpiredProposalsAsync();

        Assert.Equal(1, count);

        var storedExpired = await _context.Proposals.FindAsync(expiredProposal.Id);
        Assert.Equal(ProposalStatus.Expired, storedExpired!.Status);
        Assert.Null(storedExpired.PendingResponseFromUserId);

        var storedActive = await _context.Proposals.FindAsync(activeProposal.Id);
        Assert.Equal(ProposalStatus.PendingOfferOwnerReview, storedActive!.Status);

        var history = await _context.ProposalHistories.Where(h => h.ProposalId == expiredProposal.Id).ToListAsync();
        Assert.Single(history);
        Assert.Equal(ProposalAction.Expired, history.First().Action);
    }

    [Fact]
    public async Task GetUserProposalsAsync_FiltersBySender()
    {
        var (proposer, offerOwner, offer, _) = await SeedOfferAsync();

        var sentProposal = new Proposal
        {
            Id = Guid.NewGuid(),
            OfferId = offer.Id,
            ProposerId = proposer.Id,
            OfferOwnerId = offerOwner.Id,
            Terms = "Sent",
            ProposerOffer = "Offer",
            Deadline = DateTime.UtcNow.AddDays(5),
            Status = ProposalStatus.PendingOfferOwnerReview,
            Offer = offer,
            Proposer = proposer,
            OfferOwner = offerOwner
        };

        _context.Proposals.Add(sentProposal);
        await _context.SaveChangesAsync();

        var request = new GetProposalsRequest { AsSender = true, Page = 1, PageSize = 10 };
        var result = await _proposalService.GetUserProposalsAsync(proposer.Id, request);

        Assert.Single(result.Proposals);
        Assert.Equal(sentProposal.Id, result.Proposals.First().Id);
    }

    [Fact]
    public async Task GetOfferProposalsAsync_FiltersCorrectly()
    {
        var (proposer, offerOwner, offer, _) = await SeedOfferAsync();

        var proposal = new Proposal
        {
            Id = Guid.NewGuid(),
            OfferId = offer.Id,
            ProposerId = proposer.Id,
            OfferOwnerId = offerOwner.Id,
            Terms = "Terms",
            ProposerOffer = "Offer",
            Deadline = DateTime.UtcNow.AddDays(5),
            Status = ProposalStatus.PendingOfferOwnerReview,
            Offer = offer,
            Proposer = proposer,
            OfferOwner = offerOwner
        };
        _context.Proposals.Add(proposal);
        await _context.SaveChangesAsync();

        var request = new GetProposalsRequest { Page = 1, PageSize = 10 };
        var result = await _proposalService.GetOfferProposalsAsync(offer.Id, request);

        Assert.Single(result.Proposals);
        Assert.Equal(proposal.Id, result.Proposals.First().Id);
    }
}
