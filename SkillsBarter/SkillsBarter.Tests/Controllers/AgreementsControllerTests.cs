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

public class AgreementsControllerTests
{
    private readonly Mock<IAgreementService> _agreementServiceMock = new();
    private readonly Mock<UserManager<ApplicationUser>> _userManagerMock;
    private readonly Mock<ILogger<AgreementsController>> _loggerMock = new();
    private readonly AgreementsController _controller;
    private readonly ApplicationUser _currentUser;

    public AgreementsControllerTests()
    {
        _userManagerMock = IdentityMocks.CreateUserManager<ApplicationUser>();

        _currentUser = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Name = "Test User",
            UserName = "testuser",
            Email = "test@example.com",
            IsModerator = false
        };

        _controller = new AgreementsController(
            _agreementServiceMock.Object,
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
    public async Task CreateAgreement_InvalidModelState_ReturnsBadRequest()
    {
        _controller.ModelState.AddModelError("OfferId", "Required");

        var result = await _controller.CreateAgreement(new CreateAgreementRequest());

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Invalid request", GetMessage(badRequest.Value));
    }

    [Fact]
    public async Task CreateAgreement_UserNotFound_ReturnsUnauthorized()
    {
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync((ApplicationUser?)null);

        var request = new CreateAgreementRequest
        {
            OfferId = Guid.NewGuid(),
            RequesterId = Guid.NewGuid(),
            ProviderId = Guid.NewGuid(),
            Terms = "Test terms"
        };

        var result = await _controller.CreateAgreement(request);

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal("User not authenticated", GetMessage(unauthorized.Value));
    }

    [Fact]
    public async Task CreateAgreement_UserNotParty_ReturnsForbid()
    {
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(_currentUser);

        var request = new CreateAgreementRequest
        {
            OfferId = Guid.NewGuid(),
            RequesterId = Guid.NewGuid(),
            ProviderId = Guid.NewGuid(),
            Terms = "Test terms"
        };

        var result = await _controller.CreateAgreement(request);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task CreateAgreement_ServiceReturnsNull_ReturnsBadRequest()
    {
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(_currentUser);

        var request = new CreateAgreementRequest
        {
            OfferId = Guid.NewGuid(),
            RequesterId = _currentUser.Id,
            ProviderId = Guid.NewGuid(),
            Terms = "Test terms"
        };

        _agreementServiceMock.Setup(s =>
                s.CreateAgreementAsync(request.OfferId, request.RequesterId, request.ProviderId, request.Terms))
            .ReturnsAsync((AgreementResponse?)null);

        var result = await _controller.CreateAgreement(request);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(
            "Failed to create agreement. Ensure the offer is active, you are authorized (one party must be the offer owner), no active agreement exists, and all users are valid.",
            GetMessage(badRequest.Value));
    }

    [Fact]
    public async Task CreateAgreement_Success_ReturnsCreatedAtAction()
    {
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(_currentUser);

        var request = new CreateAgreementRequest
        {
            OfferId = Guid.NewGuid(),
            RequesterId = _currentUser.Id,
            ProviderId = Guid.NewGuid(),
            Terms = "Test terms"
        };

        var agreementResponse = new AgreementResponse
        {
            Id = Guid.NewGuid(),
            OfferId = request.OfferId,
            RequesterId = request.RequesterId,
            ProviderId = request.ProviderId,
            Terms = request.Terms,
            Status = AgreementStatus.InProgress
        };

        _agreementServiceMock.Setup(s =>
                s.CreateAgreementAsync(request.OfferId, request.RequesterId, request.ProviderId, request.Terms))
            .ReturnsAsync(agreementResponse);

        var result = await _controller.CreateAgreement(request);

        var created = Assert.IsType<CreatedAtActionResult>(result);
        Assert.Equal(nameof(AgreementsController.GetAgreement), created.ActionName);
        Assert.Equal(agreementResponse, created.Value);
    }

    [Fact]
    public async Task CreateAgreement_OnException_ReturnsServerError()
    {
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(_currentUser);

        var request = new CreateAgreementRequest
        {
            OfferId = Guid.NewGuid(),
            RequesterId = _currentUser.Id,
            ProviderId = Guid.NewGuid(),
            Terms = "Test terms"
        };

        _agreementServiceMock.Setup(s =>
                s.CreateAgreementAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string?>()))
            .ThrowsAsync(new Exception("database error"));

        var result = await _controller.CreateAgreement(request);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, objectResult.StatusCode);
        Assert.Equal("An error occurred while creating the agreement", GetMessage(objectResult.Value));
    }

    [Fact]
    public async Task GetAgreement_InvalidId_ReturnsBadRequest()
    {
        var result = await _controller.GetAgreement(Guid.Empty);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Invalid agreement ID", GetMessage(badRequest.Value));
    }

    [Fact]
    public async Task GetAgreement_UserNotFound_ReturnsUnauthorized()
    {
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync((ApplicationUser?)null);

        var result = await _controller.GetAgreement(Guid.NewGuid());

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal("User not authenticated", GetMessage(unauthorized.Value));
    }

    [Fact]
    public async Task GetAgreement_NotFound_ReturnsNotFound()
    {
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(_currentUser);
        _agreementServiceMock.Setup(s => s.GetAgreementDetailByIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync((AgreementDetailResponse?)null);

        var result = await _controller.GetAgreement(Guid.NewGuid());

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal("Agreement not found", GetMessage(notFound.Value));
    }

    [Fact]
    public async Task GetAgreement_UserNotAuthorized_ReturnsForbid()
    {
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(_currentUser);

        var agreementId = Guid.NewGuid();
        var agreementDetail = new AgreementDetailResponse
        {
            Id = agreementId,
            OfferId = Guid.NewGuid(),
            RequesterId = Guid.NewGuid(),
            ProviderId = Guid.NewGuid(),
            Status = AgreementStatus.InProgress,
            Requester = new AgreementUserInfo(),
            Provider = new AgreementUserInfo(),
            Offer = new AgreementOfferInfo()
        };

        _agreementServiceMock.Setup(s => s.GetAgreementDetailByIdAsync(agreementId))
            .ReturnsAsync(agreementDetail);

        var result = await _controller.GetAgreement(agreementId);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task GetAgreement_Success_ReturnsOk()
    {
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(_currentUser);

        var agreementId = Guid.NewGuid();
        var agreementDetail = new AgreementDetailResponse
        {
            Id = agreementId,
            OfferId = Guid.NewGuid(),
            RequesterId = _currentUser.Id,
            ProviderId = Guid.NewGuid(),
            Status = AgreementStatus.InProgress,
            Requester = new AgreementUserInfo(),
            Provider = new AgreementUserInfo(),
            Offer = new AgreementOfferInfo()
        };

        _agreementServiceMock.Setup(s => s.GetAgreementDetailByIdAsync(agreementId))
            .ReturnsAsync(agreementDetail);

        var result = await _controller.GetAgreement(agreementId);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(agreementDetail, okResult.Value);
    }

    [Fact]
    public async Task GetAgreement_OnException_ReturnsServerError()
    {
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(_currentUser);

        _agreementServiceMock.Setup(s => s.GetAgreementDetailByIdAsync(It.IsAny<Guid>()))
            .ThrowsAsync(new Exception("db error"));

        var result = await _controller.GetAgreement(Guid.NewGuid());

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, objectResult.StatusCode);
        Assert.Equal("An error occurred while retrieving the agreement", GetMessage(objectResult.Value));
    }

    [Fact]
    public async Task CompleteAgreement_InvalidId_ReturnsBadRequest()
    {
        var result = await _controller.CompleteAgreement(Guid.Empty);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Invalid agreement ID", GetMessage(badRequest.Value));
    }

    [Fact]
    public async Task CompleteAgreement_UserNotFound_ReturnsUnauthorized()
    {
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync((ApplicationUser?)null);

        var result = await _controller.CompleteAgreement(Guid.NewGuid());

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal("User not authenticated", GetMessage(unauthorized.Value));
    }

    [Fact]
    public async Task CompleteAgreement_ServiceReturnsNull_ReturnsBadRequest()
    {
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(_currentUser);
        _agreementServiceMock.Setup(s => s.CompleteAgreementAsync(It.IsAny<Guid>(), _currentUser.Id))
            .ReturnsAsync((AgreementResponse?)null);

        var result = await _controller.CompleteAgreement(Guid.NewGuid());

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(
            "Failed to complete agreement. You may not be authorized or the agreement may already be completed.",
            GetMessage(badRequest.Value));
    }

    [Fact]
    public async Task CompleteAgreement_Success_ReturnsOk()
    {
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(_currentUser);

        var agreementId = Guid.NewGuid();
        var agreementResponse = new AgreementResponse
        {
            Id = agreementId,
            OfferId = Guid.NewGuid(),
            RequesterId = _currentUser.Id,
            ProviderId = Guid.NewGuid(),
            Status = AgreementStatus.Completed
        };

        _agreementServiceMock.Setup(s => s.CompleteAgreementAsync(agreementId, _currentUser.Id))
            .ReturnsAsync(agreementResponse);

        var result = await _controller.CompleteAgreement(agreementId);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.True(
            (bool?)okResult.Value?.GetType().GetProperty("success")?.GetValue(okResult.Value)
        );
        Assert.Equal(agreementResponse, okResult.Value?.GetType().GetProperty("agreement")?.GetValue(okResult.Value));
    }

    [Fact]
    public async Task CompleteAgreement_OnException_ReturnsServerError()
    {
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(_currentUser);

        _agreementServiceMock.Setup(s => s.CompleteAgreementAsync(It.IsAny<Guid>(), _currentUser.Id))
            .ThrowsAsync(new Exception("db error"));

        var result = await _controller.CompleteAgreement(Guid.NewGuid());

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, objectResult.StatusCode);
        Assert.Equal("An error occurred while completing the agreement", GetMessage(objectResult.Value));
    }

    private static string? GetMessage(object? value) =>
        value?.GetType().GetProperty("message")?.GetValue(value)?.ToString();
}
