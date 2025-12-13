using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using SkillsBarter.Controllers;
using SkillsBarter.DTOs;
using SkillsBarter.Models;
using SkillsBarter.Services;
using SkillsBarter.Tests.TestUtils;
using Xunit;

namespace SkillsBarter.Tests.Controllers;

public class ProposalsControllerTests
{
    private readonly Mock<IProposalService> _proposalServiceMock = new();
    private readonly Mock<UserManager<ApplicationUser>> _userManagerMock;
    private readonly Mock<ILogger<ProposalsController>> _loggerMock = new();
    private readonly ProposalsController _controller;
    private readonly ApplicationUser _currentUser;

    public ProposalsControllerTests()
    {
        _userManagerMock = IdentityMocks.CreateUserManager<ApplicationUser>();

        _currentUser = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = "testuser",
            Name = "Test User",
            Email = "test@example.com",
            IsModerator = false
        };

        _controller = new ProposalsController(
            _proposalServiceMock.Object,
            _userManagerMock.Object,
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
    public async Task CreateProposal_InvalidModelState_ReturnsBadRequest()
    {
        _controller.ModelState.AddModelError("OfferId", "Required");

        var result = await _controller.CreateProposal(new CreateProposalRequest());

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Invalid request", GetMessage(badRequest.Value));
    }

    [Fact]
    public async Task CreateProposal_UserNotAuthenticated_ReturnsUnauthorized()
    {
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync((ApplicationUser?)null);

        var request = new CreateProposalRequest
        {
            OfferId = Guid.NewGuid(),
            Terms = "Test terms content",
            ProposerOffer = "Test proposer offer",
            Deadline = DateTime.UtcNow.AddDays(7)
        };

        var result = await _controller.CreateProposal(request);

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal("User not authenticated", GetMessage(unauthorized.Value));
    }

    [Fact]
    public async Task CreateProposal_ServiceReturnsNull_ReturnsBadRequest()
    {
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(_currentUser);

        var request = new CreateProposalRequest
        {
            OfferId = Guid.NewGuid(),
            Terms = "Test terms content",
            ProposerOffer = "Test proposer offer",
            Deadline = DateTime.UtcNow.AddDays(7)
        };

        _proposalServiceMock.Setup(s => s.CreateProposalAsync(request, _currentUser.Id))
            .ReturnsAsync((ProposalResponse?)null);

        var result = await _controller.CreateProposal(request);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("Failed to create proposal", GetMessage(badRequest.Value));
    }

    [Fact]
    public async Task CreateProposal_Success_ReturnsCreatedAtAction()
    {
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(_currentUser);

        var request = new CreateProposalRequest
        {
            OfferId = Guid.NewGuid(),
            Terms = "Test terms content",
            ProposerOffer = "Test proposer offer",
            Deadline = DateTime.UtcNow.AddDays(7)
        };

        var proposalResponse = new ProposalResponse
        {
            Id = Guid.NewGuid(),
            OfferId = request.OfferId,
            ProposerId = _currentUser.Id,
            Terms = request.Terms,
            ProposerOffer = request.ProposerOffer,
            Status = ProposalStatus.PendingOfferOwnerReview
        };

        _proposalServiceMock.Setup(s => s.CreateProposalAsync(request, _currentUser.Id))
            .ReturnsAsync(proposalResponse);

        var result = await _controller.CreateProposal(request);

        var created = Assert.IsType<CreatedAtActionResult>(result);
        Assert.Equal(nameof(ProposalsController.GetProposal), created.ActionName);
        Assert.Equal(proposalResponse, created.Value);
    }

    [Fact]
    public async Task CreateProposal_OnException_ReturnsServerError()
    {
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(_currentUser);

        var request = new CreateProposalRequest
        {
            OfferId = Guid.NewGuid(),
            Terms = "Test terms content",
            ProposerOffer = "Test proposer offer",
            Deadline = DateTime.UtcNow.AddDays(7)
        };

        _proposalServiceMock.Setup(s => s.CreateProposalAsync(It.IsAny<CreateProposalRequest>(), It.IsAny<Guid>()))
            .ThrowsAsync(new Exception("Database error"));

        var result = await _controller.CreateProposal(request);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, objectResult.StatusCode);
        Assert.Equal("An error occurred while creating the proposal", GetMessage(objectResult.Value));
    }

    [Fact]
    public async Task GetProposal_InvalidId_ReturnsBadRequest()
    {
        var result = await _controller.GetProposal(Guid.Empty);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Invalid proposal ID", GetMessage(badRequest.Value));
    }

    [Fact]
    public async Task GetProposal_UserNotAuthenticated_ReturnsUnauthorized()
    {
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync((ApplicationUser?)null);

        var result = await _controller.GetProposal(Guid.NewGuid());

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal("User not authenticated", GetMessage(unauthorized.Value));
    }

    [Fact]
    public async Task GetProposal_NotFound_ReturnsNotFound()
    {
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(_currentUser);
        _proposalServiceMock.Setup(s => s.GetProposalDetailByIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync((ProposalDetailResponse?)null);

        var result = await _controller.GetProposal(Guid.NewGuid());

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal("Proposal not found", GetMessage(notFound.Value));
    }

    [Fact]
    public async Task GetProposal_UserNotAuthorized_ReturnsForbid()
    {
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(_currentUser);

        var proposalId = Guid.NewGuid();
        var proposal = new ProposalDetailResponse
        {
            Id = proposalId,
            ProposerId = Guid.NewGuid(),
            OfferOwnerId = Guid.NewGuid(),
            Proposer = new ProposalUserInfo(),
            OfferOwner = new ProposalUserInfo(),
            Offer = new ProposalOfferInfo()
        };

        _proposalServiceMock.Setup(s => s.GetProposalDetailByIdAsync(proposalId))
            .ReturnsAsync(proposal);

        var result = await _controller.GetProposal(proposalId);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task GetProposal_AsProposer_ReturnsOk()
    {
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(_currentUser);

        var proposalId = Guid.NewGuid();
        var proposal = new ProposalDetailResponse
        {
            Id = proposalId,
            ProposerId = _currentUser.Id,
            OfferOwnerId = Guid.NewGuid(),
            Proposer = new ProposalUserInfo(),
            OfferOwner = new ProposalUserInfo(),
            Offer = new ProposalOfferInfo()
        };

        _proposalServiceMock.Setup(s => s.GetProposalDetailByIdAsync(proposalId))
            .ReturnsAsync(proposal);

        var result = await _controller.GetProposal(proposalId);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(proposal, ok.Value);
    }

    [Fact]
    public async Task GetProposal_AsModerator_ReturnsOk()
    {
        var moderator = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            IsModerator = true
        };
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(moderator);

        var proposalId = Guid.NewGuid();
        var proposal = new ProposalDetailResponse
        {
            Id = proposalId,
            ProposerId = Guid.NewGuid(),
            OfferOwnerId = Guid.NewGuid(),
            Proposer = new ProposalUserInfo(),
            OfferOwner = new ProposalUserInfo(),
            Offer = new ProposalOfferInfo()
        };

        _proposalServiceMock.Setup(s => s.GetProposalDetailByIdAsync(proposalId))
            .ReturnsAsync(proposal);

        var result = await _controller.GetProposal(proposalId);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(proposal, ok.Value);
    }

    [Fact]
    public async Task RespondToProposal_InvalidId_ReturnsBadRequest()
    {
        var result = await _controller.RespondToProposal(Guid.Empty, new RespondToProposalRequest());

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Invalid proposal ID", GetMessage(badRequest.Value));
    }

    [Fact]
    public async Task RespondToProposal_UserNotAuthenticated_ReturnsUnauthorized()
    {
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync((ApplicationUser?)null);

        var result = await _controller.RespondToProposal(Guid.NewGuid(), new RespondToProposalRequest { Action = ProposalResponseAction.Accept });

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal("User not authenticated", GetMessage(unauthorized.Value));
    }

    [Fact]
    public async Task RespondToProposal_CannotRespond_ReturnsBadRequest()
    {
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(_currentUser);

        var proposalId = Guid.NewGuid();
        _proposalServiceMock.Setup(s => s.CanUserRespondAsync(proposalId, _currentUser.Id))
            .ReturnsAsync(false);

        var result = await _controller.RespondToProposal(proposalId, new RespondToProposalRequest { Action = ProposalResponseAction.Accept });

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("You cannot respond to this proposal", GetMessage(badRequest.Value));
    }

    [Fact]
    public async Task RespondToProposal_AcceptSuccess_ReturnsOkWithMessage()
    {
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(_currentUser);

        var proposalId = Guid.NewGuid();
        var request = new RespondToProposalRequest { Action = ProposalResponseAction.Accept };
        var proposalResponse = new ProposalResponse
        {
            Id = proposalId,
            Status = ProposalStatus.Accepted,
            AgreementId = Guid.NewGuid()
        };

        _proposalServiceMock.Setup(s => s.CanUserRespondAsync(proposalId, _currentUser.Id))
            .ReturnsAsync(true);
        _proposalServiceMock.Setup(s => s.RespondToProposalAsync(proposalId, request, _currentUser.Id))
            .ReturnsAsync(proposalResponse);

        var result = await _controller.RespondToProposal(proposalId, request);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.True((bool?)ok.Value?.GetType().GetProperty("success")?.GetValue(ok.Value));
        Assert.Contains("Agreement created", ok.Value?.GetType().GetProperty("message")?.GetValue(ok.Value)?.ToString());
    }

    [Fact]
    public async Task RespondToProposal_DeclineSuccess_ReturnsOk()
    {
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(_currentUser);

        var proposalId = Guid.NewGuid();
        var request = new RespondToProposalRequest { Action = ProposalResponseAction.Decline, Message = "Not interested" };
        var proposalResponse = new ProposalResponse
        {
            Id = proposalId,
            Status = ProposalStatus.Declined
        };

        _proposalServiceMock.Setup(s => s.CanUserRespondAsync(proposalId, _currentUser.Id))
            .ReturnsAsync(true);
        _proposalServiceMock.Setup(s => s.RespondToProposalAsync(proposalId, request, _currentUser.Id))
            .ReturnsAsync(proposalResponse);

        var result = await _controller.RespondToProposal(proposalId, request);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Contains("declined", ok.Value?.GetType().GetProperty("message")?.GetValue(ok.Value)?.ToString());
    }

    [Fact]
    public async Task WithdrawProposal_InvalidId_ReturnsBadRequest()
    {
        var result = await _controller.WithdrawProposal(Guid.Empty);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Invalid proposal ID", GetMessage(badRequest.Value));
    }

    [Fact]
    public async Task WithdrawProposal_UserNotAuthenticated_ReturnsUnauthorized()
    {
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync((ApplicationUser?)null);

        var result = await _controller.WithdrawProposal(Guid.NewGuid());

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal("User not authenticated", GetMessage(unauthorized.Value));
    }

    [Fact]
    public async Task WithdrawProposal_ServiceReturnsFalse_ReturnsBadRequest()
    {
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(_currentUser);

        var proposalId = Guid.NewGuid();
        _proposalServiceMock.Setup(s => s.WithdrawProposalAsync(proposalId, _currentUser.Id))
            .ReturnsAsync(false);

        var result = await _controller.WithdrawProposal(proposalId);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("Failed to withdraw proposal", GetMessage(badRequest.Value));
    }

    [Fact]
    public async Task WithdrawProposal_Success_ReturnsOk()
    {
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(_currentUser);

        var proposalId = Guid.NewGuid();
        _proposalServiceMock.Setup(s => s.WithdrawProposalAsync(proposalId, _currentUser.Id))
            .ReturnsAsync(true);

        var result = await _controller.WithdrawProposal(proposalId);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.True((bool?)ok.Value?.GetType().GetProperty("success")?.GetValue(ok.Value));
    }

    [Fact]
    public async Task GetMyProposals_UserNotAuthenticated_ReturnsUnauthorized()
    {
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync((ApplicationUser?)null);

        var result = await _controller.GetMyProposals(new GetProposalsRequest());

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal("User not authenticated", GetMessage(unauthorized.Value));
    }

    [Fact]
    public async Task GetMyProposals_Success_ReturnsOk()
    {
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(_currentUser);

        var request = new GetProposalsRequest { Page = 1, PageSize = 10 };
        var response = new ProposalListResponse
        {
            Proposals = new List<ProposalResponse>(),
            TotalCount = 0,
            Page = 1,
            PageSize = 10,
            TotalPages = 0
        };

        _proposalServiceMock.Setup(s => s.GetUserProposalsAsync(_currentUser.Id, request))
            .ReturnsAsync(response);

        var result = await _controller.GetMyProposals(request);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(response, ok.Value);
    }

    [Fact]
    public async Task GetOfferProposals_InvalidOfferId_ReturnsBadRequest()
    {
        var result = await _controller.GetOfferProposals(Guid.Empty, new GetProposalsRequest());

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Invalid offer ID", GetMessage(badRequest.Value));
    }

    [Fact]
    public async Task GetOfferProposals_Success_ReturnsOk()
    {
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(_currentUser);

        var offerId = Guid.NewGuid();
        var request = new GetProposalsRequest { Page = 1, PageSize = 10 };
        var response = new ProposalListResponse
        {
            Proposals = new List<ProposalResponse>(),
            TotalCount = 0,
            Page = 1,
            PageSize = 10,
            TotalPages = 0
        };

        _proposalServiceMock.Setup(s => s.GetOfferProposalsAsync(offerId, It.IsAny<GetProposalsRequest>()))
            .ReturnsAsync(response);

        var result = await _controller.GetOfferProposals(offerId, request);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(response, ok.Value);
    }

    [Fact]
    public async Task GetPendingProposals_UserNotAuthenticated_ReturnsUnauthorized()
    {
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync((ApplicationUser?)null);

        var result = await _controller.GetPendingProposals();

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal("User not authenticated", GetMessage(unauthorized.Value));
    }

    [Fact]
    public async Task GetPendingProposals_Success_ReturnsFilteredProposals()
    {
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(_currentUser);

        var pendingProposal = new ProposalResponse
        {
            Id = Guid.NewGuid(),
            PendingResponseFromUserId = _currentUser.Id,
            Status = ProposalStatus.PendingOfferOwnerReview
        };

        var otherProposal = new ProposalResponse
        {
            Id = Guid.NewGuid(),
            PendingResponseFromUserId = Guid.NewGuid(),
            Status = ProposalStatus.PendingOfferOwnerReview
        };

        var allProposals = new ProposalListResponse
        {
            Proposals = new List<ProposalResponse> { pendingProposal, otherProposal },
            TotalCount = 2,
            Page = 1,
            PageSize = 1000,
            TotalPages = 1
        };

        _proposalServiceMock.Setup(s => s.GetUserProposalsAsync(_currentUser.Id, It.IsAny<GetProposalsRequest>()))
            .ReturnsAsync(allProposals);

        var result = await _controller.GetPendingProposals();

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ProposalListResponse>(ok.Value);
        Assert.Single(response.Proposals);
        Assert.Equal(pendingProposal.Id, response.Proposals.First().Id);
    }

    private static string? GetMessage(object? value) =>
        value?.GetType().GetProperty("message")?.GetValue(value)?.ToString();
}
