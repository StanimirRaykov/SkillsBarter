using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Moq;
using SkillsBarter.Controllers;
using SkillsBarter.DTOs;
using SkillsBarter.Models;
using SkillsBarter.Services;
using SkillsBarter.Tests.TestUtils;
using Xunit;

namespace SkillsBarter.Tests.Controllers;

public class DisputesControllerTests
{
    private readonly Mock<IDisputeService> _disputeServiceMock = new();
    private readonly Mock<UserManager<ApplicationUser>> _userManagerMock;
    private readonly DisputesController _controller;
    private readonly ApplicationUser _currentUser;

    public DisputesControllerTests()
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

        _controller = new DisputesController(
            _disputeServiceMock.Object,
            _userManagerMock.Object
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
    public async Task OpenDispute_InvalidModelState_ReturnsBadRequest()
    {
        _controller.ModelState.AddModelError("AgreementId", "Required");

        var result = await _controller.OpenDispute(new OpenDisputeRequest());

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Invalid request", GetMessage(badRequest.Value));
    }

    [Fact]
    public async Task OpenDispute_UserNotAuthenticated_ReturnsUnauthorized()
    {
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync((ApplicationUser?)null);

        var request = new OpenDisputeRequest
        {
            AgreementId = Guid.NewGuid(),
            ReasonCode = DisputeReasonCode.WorkNotDelivered,
            Description = "Test description for the dispute"
        };

        var result = await _controller.OpenDispute(request);

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal("User not authenticated", GetMessage(unauthorized.Value));
    }

    [Fact]
    public async Task OpenDispute_ServiceReturnsNull_ReturnsBadRequest()
    {
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(_currentUser);

        var request = new OpenDisputeRequest
        {
            AgreementId = Guid.NewGuid(),
            ReasonCode = DisputeReasonCode.WorkNotDelivered,
            Description = "Test description for the dispute"
        };

        _disputeServiceMock.Setup(s => s.OpenDisputeAsync(request, _currentUser.Id))
            .ReturnsAsync((DisputeResponse?)null);

        var result = await _controller.OpenDispute(request);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("Failed to open dispute", GetMessage(badRequest.Value));
    }

    [Fact]
    public async Task OpenDispute_Success_ReturnsCreatedAtAction()
    {
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(_currentUser);

        var request = new OpenDisputeRequest
        {
            AgreementId = Guid.NewGuid(),
            ReasonCode = DisputeReasonCode.WorkNotDelivered,
            Description = "Test description for the dispute"
        };

        var disputeResponse = new DisputeResponse
        {
            Id = Guid.NewGuid(),
            AgreementId = request.AgreementId,
            ReasonCode = request.ReasonCode,
            Status = DisputeStatus.AwaitingResponse
        };

        _disputeServiceMock.Setup(s => s.OpenDisputeAsync(request, _currentUser.Id))
            .ReturnsAsync(disputeResponse);

        var result = await _controller.OpenDispute(request);

        var created = Assert.IsType<CreatedAtActionResult>(result);
        Assert.Equal(nameof(DisputesController.GetDispute), created.ActionName);
        Assert.Equal(disputeResponse, created.Value);
    }

    [Fact]
    public async Task GetDispute_UserNotAuthenticated_ReturnsUnauthorized()
    {
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync((ApplicationUser?)null);

        var result = await _controller.GetDispute(Guid.NewGuid());

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal("User not authenticated", GetMessage(unauthorized.Value));
    }

    [Fact]
    public async Task GetDispute_NotFound_ReturnsNotFound()
    {
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(_currentUser);

        var disputeId = Guid.NewGuid();
        _disputeServiceMock.Setup(s => s.GetDisputeByIdAsync(disputeId, _currentUser.Id))
            .ReturnsAsync((DisputeResponse?)null);

        var result = await _controller.GetDispute(disputeId);

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal("Dispute not found or access denied", GetMessage(notFound.Value));
    }

    [Fact]
    public async Task GetDispute_Success_ReturnsOk()
    {
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(_currentUser);

        var disputeId = Guid.NewGuid();
        var disputeResponse = new DisputeResponse
        {
            Id = disputeId,
            AgreementId = Guid.NewGuid(),
            Status = DisputeStatus.AwaitingResponse
        };

        _disputeServiceMock.Setup(s => s.GetDisputeByIdAsync(disputeId, _currentUser.Id))
            .ReturnsAsync(disputeResponse);

        var result = await _controller.GetDispute(disputeId);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(disputeResponse, ok.Value);
    }

    [Fact]
    public async Task GetMyDisputes_UserNotAuthenticated_ReturnsUnauthorized()
    {
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync((ApplicationUser?)null);

        var result = await _controller.GetMyDisputes();

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal("User not authenticated", GetMessage(unauthorized.Value));
    }

    [Fact]
    public async Task GetMyDisputes_Success_ReturnsOk()
    {
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(_currentUser);

        var disputes = new List<DisputeListResponse>
        {
            new() { Id = Guid.NewGuid(), Status = DisputeStatus.AwaitingResponse }
        };

        _disputeServiceMock.Setup(s => s.GetMyDisputesAsync(_currentUser.Id))
            .ReturnsAsync(disputes);

        var result = await _controller.GetMyDisputes();

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(disputes, ok.Value);
    }

    [Fact]
    public async Task RespondToDispute_InvalidModelState_ReturnsBadRequest()
    {
        _controller.ModelState.AddModelError("Response", "Required");

        var result = await _controller.RespondToDispute(Guid.NewGuid(), new RespondToDisputeRequest());

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Invalid request", GetMessage(badRequest.Value));
    }

    [Fact]
    public async Task RespondToDispute_UserNotAuthenticated_ReturnsUnauthorized()
    {
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync((ApplicationUser?)null);

        var result = await _controller.RespondToDispute(Guid.NewGuid(), new RespondToDisputeRequest { Response = "Test response content" });

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal("User not authenticated", GetMessage(unauthorized.Value));
    }

    [Fact]
    public async Task RespondToDispute_ServiceReturnsNull_ReturnsBadRequest()
    {
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(_currentUser);

        var disputeId = Guid.NewGuid();
        var request = new RespondToDisputeRequest { Response = "Test response content" };

        _disputeServiceMock.Setup(s => s.RespondToDisputeAsync(disputeId, request, _currentUser.Id))
            .ReturnsAsync((DisputeResponse?)null);

        var result = await _controller.RespondToDispute(disputeId, request);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("Failed to respond", GetMessage(badRequest.Value));
    }

    [Fact]
    public async Task RespondToDispute_Success_ReturnsOk()
    {
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(_currentUser);

        var disputeId = Guid.NewGuid();
        var request = new RespondToDisputeRequest { Response = "Test response content" };
        var disputeResponse = new DisputeResponse
        {
            Id = disputeId,
            Status = DisputeStatus.UnderReview
        };

        _disputeServiceMock.Setup(s => s.RespondToDisputeAsync(disputeId, request, _currentUser.Id))
            .ReturnsAsync(disputeResponse);

        var result = await _controller.RespondToDispute(disputeId, request);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal("Response submitted", ok.Value?.GetType().GetProperty("message")?.GetValue(ok.Value));
    }

    [Fact]
    public async Task AddEvidence_InvalidModelState_ReturnsBadRequest()
    {
        _controller.ModelState.AddModelError("Link", "Required");

        var result = await _controller.AddEvidence(Guid.NewGuid(), new EvidenceRequest());

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Invalid request", GetMessage(badRequest.Value));
    }

    [Fact]
    public async Task AddEvidence_UserNotAuthenticated_ReturnsUnauthorized()
    {
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync((ApplicationUser?)null);

        var result = await _controller.AddEvidence(Guid.NewGuid(), new EvidenceRequest { Link = "http://test.com", Description = "Test" });

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal("User not authenticated", GetMessage(unauthorized.Value));
    }

    [Fact]
    public async Task AddEvidence_ServiceReturnsNull_ReturnsBadRequest()
    {
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(_currentUser);

        var disputeId = Guid.NewGuid();
        var request = new EvidenceRequest { Link = "http://test.com", Description = "Test evidence" };

        _disputeServiceMock.Setup(s => s.AddEvidenceAsync(disputeId, request, _currentUser.Id))
            .ReturnsAsync((DisputeResponse?)null);

        var result = await _controller.AddEvidence(disputeId, request);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("Failed to add evidence", GetMessage(badRequest.Value));
    }

    [Fact]
    public async Task AddEvidence_Success_ReturnsOk()
    {
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(_currentUser);

        var disputeId = Guid.NewGuid();
        var request = new EvidenceRequest { Link = "http://test.com", Description = "Test evidence" };
        var disputeResponse = new DisputeResponse
        {
            Id = disputeId,
            Evidence = new List<EvidenceResponse> { new() { Link = request.Link } }
        };

        _disputeServiceMock.Setup(s => s.AddEvidenceAsync(disputeId, request, _currentUser.Id))
            .ReturnsAsync(disputeResponse);

        var result = await _controller.AddEvidence(disputeId, request);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal("Evidence added", ok.Value?.GetType().GetProperty("message")?.GetValue(ok.Value));
    }

    [Fact]
    public async Task GetDisputesForModeration_UserNotAuthenticated_ReturnsUnauthorized()
    {
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync((ApplicationUser?)null);

        var result = await _controller.GetDisputesForModeration();

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal("User not authenticated", GetMessage(unauthorized.Value));
    }

    [Fact]
    public async Task GetDisputesForModeration_NotModerator_ReturnsForbid()
    {
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(_currentUser);

        var result = await _controller.GetDisputesForModeration();

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task GetDisputesForModeration_AsModerator_ReturnsOk()
    {
        var moderator = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            IsModerator = true
        };
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(moderator);

        var disputes = new List<DisputeListResponse>
        {
            new() { Id = Guid.NewGuid(), Status = DisputeStatus.EscalatedToModerator }
        };

        _disputeServiceMock.Setup(s => s.GetDisputesForModerationAsync(moderator.Id))
            .ReturnsAsync(disputes);

        var result = await _controller.GetDisputesForModeration();

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(disputes, ok.Value);
    }

    [Fact]
    public async Task MakeModeratorDecision_InvalidModelState_ReturnsBadRequest()
    {
        _controller.ModelState.AddModelError("Notes", "Required");

        var result = await _controller.MakeModeratorDecision(Guid.NewGuid(), new ModeratorDecisionRequest());

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Invalid request", GetMessage(badRequest.Value));
    }

    [Fact]
    public async Task MakeModeratorDecision_UserNotAuthenticated_ReturnsUnauthorized()
    {
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync((ApplicationUser?)null);

        var result = await _controller.MakeModeratorDecision(Guid.NewGuid(), new ModeratorDecisionRequest
        {
            Resolution = DisputeResolution.FavorsComplainer,
            Notes = "Test moderator notes"
        });

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal("User not authenticated", GetMessage(unauthorized.Value));
    }

    [Fact]
    public async Task MakeModeratorDecision_NotModerator_ReturnsForbid()
    {
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(_currentUser);

        var result = await _controller.MakeModeratorDecision(Guid.NewGuid(), new ModeratorDecisionRequest
        {
            Resolution = DisputeResolution.FavorsComplainer,
            Notes = "Test moderator notes"
        });

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task MakeModeratorDecision_ServiceReturnsNull_ReturnsBadRequest()
    {
        var moderator = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            IsModerator = true
        };
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(moderator);

        var disputeId = Guid.NewGuid();
        var request = new ModeratorDecisionRequest
        {
            Resolution = DisputeResolution.FavorsComplainer,
            Notes = "Test moderator notes"
        };

        _disputeServiceMock.Setup(s => s.MakeModeratorDecisionAsync(disputeId, request, moderator.Id))
            .ReturnsAsync((DisputeResponse?)null);

        var result = await _controller.MakeModeratorDecision(disputeId, request);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("Failed to make decision", GetMessage(badRequest.Value));
    }

    [Fact]
    public async Task MakeModeratorDecision_Success_ReturnsOk()
    {
        var moderator = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            IsModerator = true
        };
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(moderator);

        var disputeId = Guid.NewGuid();
        var request = new ModeratorDecisionRequest
        {
            Resolution = DisputeResolution.FavorsComplainer,
            Notes = "Test moderator notes"
        };
        var disputeResponse = new DisputeResponse
        {
            Id = disputeId,
            Status = DisputeStatus.Resolved,
            Resolution = DisputeResolution.FavorsComplainer
        };

        _disputeServiceMock.Setup(s => s.MakeModeratorDecisionAsync(disputeId, request, moderator.Id))
            .ReturnsAsync(disputeResponse);

        var result = await _controller.MakeModeratorDecision(disputeId, request);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal("Decision recorded", ok.Value?.GetType().GetProperty("message")?.GetValue(ok.Value));
    }

    private static string? GetMessage(object? value) =>
        value?.GetType().GetProperty("message")?.GetValue(value)?.ToString();
}
