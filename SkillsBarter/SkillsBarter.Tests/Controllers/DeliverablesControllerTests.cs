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

public class DeliverablesControllerTests
{
    private readonly Mock<IDeliverableService> _deliverableServiceMock = new();
    private readonly Mock<UserManager<ApplicationUser>> _userManagerMock;
    private readonly Mock<ILogger<DeliverablesController>> _loggerMock = new();
    private readonly DeliverablesController _controller;
    private readonly ApplicationUser _currentUser;

    public DeliverablesControllerTests()
    {
        _userManagerMock = IdentityMocks.CreateUserManager<ApplicationUser>();

        _currentUser = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = "testuser",
            Name = "Test User",
            Email = "test@example.com"
        };

        _controller = new DeliverablesController(
            _deliverableServiceMock.Object,
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
    public async Task SubmitDeliverable_InvalidModelState_ReturnsBadRequest()
    {
        _controller.ModelState.AddModelError("Link", "Required");

        var result = await _controller.SubmitDeliverable(new SubmitDeliverableRequest());

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Invalid request", GetMessage(badRequest.Value));
    }

    [Fact]
    public async Task SubmitDeliverable_UserNotAuthenticated_ReturnsUnauthorized()
    {
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync((ApplicationUser?)null);

        var request = new SubmitDeliverableRequest
        {
            AgreementId = Guid.NewGuid(),
            Link = "http://deliverable.com/file",
            Description = "Test deliverable"
        };

        var result = await _controller.SubmitDeliverable(request);

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal("User not authenticated", GetMessage(unauthorized.Value));
    }

    [Fact]
    public async Task SubmitDeliverable_ServiceReturnsNull_ReturnsBadRequest()
    {
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(_currentUser);

        var request = new SubmitDeliverableRequest
        {
            AgreementId = Guid.NewGuid(),
            Link = "http://deliverable.com/file",
            Description = "Test deliverable"
        };

        _deliverableServiceMock.Setup(s => s.SubmitDeliverableAsync(request, _currentUser.Id))
            .ReturnsAsync((DeliverableResponse?)null);

        var result = await _controller.SubmitDeliverable(request);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("Failed to submit", GetMessage(badRequest.Value));
    }

    [Fact]
    public async Task SubmitDeliverable_Success_ReturnsCreatedAtAction()
    {
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(_currentUser);

        var request = new SubmitDeliverableRequest
        {
            AgreementId = Guid.NewGuid(),
            Link = "http://deliverable.com/file",
            Description = "Test deliverable"
        };

        var response = new DeliverableResponse
        {
            Id = Guid.NewGuid(),
            AgreementId = request.AgreementId,
            Link = request.Link,
            Status = DeliverableStatus.Submitted
        };

        _deliverableServiceMock.Setup(s => s.SubmitDeliverableAsync(request, _currentUser.Id))
            .ReturnsAsync(response);

        var result = await _controller.SubmitDeliverable(request);

        var created = Assert.IsType<CreatedAtActionResult>(result);
        Assert.Equal(nameof(DeliverablesController.GetDeliverable), created.ActionName);
        Assert.Equal(response, created.Value);
    }

    [Fact]
    public async Task GetDeliverable_UserNotAuthenticated_ReturnsUnauthorized()
    {
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync((ApplicationUser?)null);

        var result = await _controller.GetDeliverable(Guid.NewGuid());

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal("User not authenticated", GetMessage(unauthorized.Value));
    }

    [Fact]
    public async Task GetDeliverable_NotFound_ReturnsNotFound()
    {
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(_currentUser);

        var deliverableId = Guid.NewGuid();
        _deliverableServiceMock.Setup(s => s.GetDeliverableByIdAsync(deliverableId, _currentUser.Id))
            .ReturnsAsync((DeliverableResponse?)null);

        var result = await _controller.GetDeliverable(deliverableId);

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal("Deliverable not found or access denied", GetMessage(notFound.Value));
    }

    [Fact]
    public async Task GetDeliverable_Success_ReturnsOk()
    {
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(_currentUser);

        var deliverableId = Guid.NewGuid();
        var response = new DeliverableResponse
        {
            Id = deliverableId,
            Status = DeliverableStatus.Submitted
        };

        _deliverableServiceMock.Setup(s => s.GetDeliverableByIdAsync(deliverableId, _currentUser.Id))
            .ReturnsAsync(response);

        var result = await _controller.GetDeliverable(deliverableId);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(response, ok.Value);
    }

    [Fact]
    public async Task GetAgreementDeliverables_UserNotAuthenticated_ReturnsUnauthorized()
    {
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync((ApplicationUser?)null);

        var result = await _controller.GetAgreementDeliverables(Guid.NewGuid());

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal("User not authenticated", GetMessage(unauthorized.Value));
    }

    [Fact]
    public async Task GetAgreementDeliverables_NotFound_ReturnsNotFound()
    {
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(_currentUser);

        var agreementId = Guid.NewGuid();
        _deliverableServiceMock.Setup(s => s.GetAgreementDeliverablesAsync(agreementId, _currentUser.Id))
            .ReturnsAsync((AgreementDeliverablesResponse?)null);

        var result = await _controller.GetAgreementDeliverables(agreementId);

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal("Agreement not found or access denied", GetMessage(notFound.Value));
    }

    [Fact]
    public async Task GetAgreementDeliverables_Success_ReturnsOk()
    {
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(_currentUser);

        var agreementId = Guid.NewGuid();
        var response = new AgreementDeliverablesResponse
        {
            AgreementId = agreementId,
            AllApproved = false
        };

        _deliverableServiceMock.Setup(s => s.GetAgreementDeliverablesAsync(agreementId, _currentUser.Id))
            .ReturnsAsync(response);

        var result = await _controller.GetAgreementDeliverables(agreementId);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(response, ok.Value);
    }

    [Fact]
    public async Task ApproveDeliverable_UserNotAuthenticated_ReturnsUnauthorized()
    {
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync((ApplicationUser?)null);

        var result = await _controller.ApproveDeliverable(Guid.NewGuid());

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal("User not authenticated", GetMessage(unauthorized.Value));
    }

    [Fact]
    public async Task ApproveDeliverable_ServiceReturnsNull_ReturnsBadRequest()
    {
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(_currentUser);

        var deliverableId = Guid.NewGuid();
        _deliverableServiceMock.Setup(s => s.ApproveDeliverableAsync(deliverableId, _currentUser.Id))
            .ReturnsAsync((DeliverableResponse?)null);

        var result = await _controller.ApproveDeliverable(deliverableId);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("Failed to approve", GetMessage(badRequest.Value));
    }

    [Fact]
    public async Task ApproveDeliverable_Success_ReturnsOk()
    {
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(_currentUser);

        var deliverableId = Guid.NewGuid();
        var response = new DeliverableResponse
        {
            Id = deliverableId,
            Status = DeliverableStatus.Approved
        };

        _deliverableServiceMock.Setup(s => s.ApproveDeliverableAsync(deliverableId, _currentUser.Id))
            .ReturnsAsync(response);

        var result = await _controller.ApproveDeliverable(deliverableId);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal("Deliverable approved", ok.Value?.GetType().GetProperty("message")?.GetValue(ok.Value));
    }

    [Fact]
    public async Task RequestRevision_InvalidModelState_ReturnsBadRequest()
    {
        _controller.ModelState.AddModelError("Reason", "Required");

        var result = await _controller.RequestRevision(Guid.NewGuid(), new RequestRevisionRequest());

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Invalid request", GetMessage(badRequest.Value));
    }

    [Fact]
    public async Task RequestRevision_UserNotAuthenticated_ReturnsUnauthorized()
    {
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync((ApplicationUser?)null);

        var result = await _controller.RequestRevision(Guid.NewGuid(), new RequestRevisionRequest { Reason = "Needs improvement" });

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal("User not authenticated", GetMessage(unauthorized.Value));
    }

    [Fact]
    public async Task RequestRevision_ServiceReturnsNull_ReturnsBadRequest()
    {
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(_currentUser);

        var deliverableId = Guid.NewGuid();
        var request = new RequestRevisionRequest { Reason = "Needs improvement" };

        _deliverableServiceMock.Setup(s => s.RequestRevisionAsync(deliverableId, request, _currentUser.Id))
            .ReturnsAsync((DeliverableResponse?)null);

        var result = await _controller.RequestRevision(deliverableId, request);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("Failed to request revision", GetMessage(badRequest.Value));
    }

    [Fact]
    public async Task RequestRevision_Success_ReturnsOk()
    {
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(_currentUser);

        var deliverableId = Guid.NewGuid();
        var request = new RequestRevisionRequest { Reason = "Needs improvement" };
        var response = new DeliverableResponse
        {
            Id = deliverableId,
            Status = DeliverableStatus.RevisionRequested,
            RevisionReason = request.Reason
        };

        _deliverableServiceMock.Setup(s => s.RequestRevisionAsync(deliverableId, request, _currentUser.Id))
            .ReturnsAsync(response);

        var result = await _controller.RequestRevision(deliverableId, request);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal("Revision requested", ok.Value?.GetType().GetProperty("message")?.GetValue(ok.Value));
    }

    [Fact]
    public async Task ResubmitDeliverable_InvalidModelState_ReturnsBadRequest()
    {
        _controller.ModelState.AddModelError("Link", "Required");

        var result = await _controller.ResubmitDeliverable(Guid.NewGuid(), new SubmitDeliverableRequest());

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Invalid request", GetMessage(badRequest.Value));
    }

    [Fact]
    public async Task ResubmitDeliverable_UserNotAuthenticated_ReturnsUnauthorized()
    {
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync((ApplicationUser?)null);

        var request = new SubmitDeliverableRequest
        {
            AgreementId = Guid.NewGuid(),
            Link = "http://deliverable.com/revised",
            Description = "Revised deliverable"
        };

        var result = await _controller.ResubmitDeliverable(Guid.NewGuid(), request);

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal("User not authenticated", GetMessage(unauthorized.Value));
    }

    [Fact]
    public async Task ResubmitDeliverable_ServiceReturnsNull_ReturnsBadRequest()
    {
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(_currentUser);

        var deliverableId = Guid.NewGuid();
        var request = new SubmitDeliverableRequest
        {
            AgreementId = Guid.NewGuid(),
            Link = "http://deliverable.com/revised",
            Description = "Revised deliverable"
        };

        _deliverableServiceMock.Setup(s => s.ResubmitDeliverableAsync(deliverableId, request, _currentUser.Id))
            .ReturnsAsync((DeliverableResponse?)null);

        var result = await _controller.ResubmitDeliverable(deliverableId, request);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("Failed to resubmit", GetMessage(badRequest.Value));
    }

    [Fact]
    public async Task ResubmitDeliverable_Success_ReturnsOk()
    {
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(_currentUser);

        var deliverableId = Guid.NewGuid();
        var request = new SubmitDeliverableRequest
        {
            AgreementId = Guid.NewGuid(),
            Link = "http://deliverable.com/revised",
            Description = "Revised deliverable"
        };
        var response = new DeliverableResponse
        {
            Id = deliverableId,
            Link = request.Link,
            Status = DeliverableStatus.Submitted
        };

        _deliverableServiceMock.Setup(s => s.ResubmitDeliverableAsync(deliverableId, request, _currentUser.Id))
            .ReturnsAsync(response);

        var result = await _controller.ResubmitDeliverable(deliverableId, request);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal("Deliverable resubmitted", ok.Value?.GetType().GetProperty("message")?.GetValue(ok.Value));
    }

    private static string? GetMessage(object? value) =>
        value?.GetType().GetProperty("message")?.GetValue(value)?.ToString();
}
