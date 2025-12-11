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

public class OffersControllerTests
{
    private readonly Mock<IOfferService> _offerServiceMock = new();
    private readonly Mock<UserManager<ApplicationUser>> _userManagerMock;
    private readonly Mock<ILogger<OffersController>> _loggerMock = new();
    private readonly OffersController _controller;
    private readonly ApplicationUser _testUser;

    public OffersControllerTests()
    {
        _userManagerMock = IdentityMocks.CreateUserManager<ApplicationUser>();

        _testUser = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Name = "Test User",
            UserName = "testuser",
            Email = "test@example.com"
        };

        _controller = new OffersController(
            _offerServiceMock.Object,
            _userManagerMock.Object,
            _loggerMock.Object);

        SetupControllerContext();
    }

    private void SetupControllerContext()
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, _testUser.Id.ToString()),
            new Claim(ClaimTypes.Name, _testUser.UserName!)
        };
        var identity = new ClaimsIdentity(claims, "TestAuthentication");
        var claimsPrincipal = new ClaimsPrincipal(identity);

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = claimsPrincipal }
        };
    }

    [Fact]
    public async Task GetOffers_ReturnsOkWithData()
    {
        var request = new GetOffersRequest { Page = 1, PageSize = 10 };
        var expectedResponse = new PaginatedResponse<OfferResponse>
        {
            Items = [new OfferResponse
            {
                Id = Guid.NewGuid(),
                UserId = _testUser.Id,
                SkillId = 1,
                Title = "Test Offer",
                Description = "Test Description",
                StatusCode = "Active",
                StatusLabel = "Active"
            }],
            Page = 1,
            PageSize = 10,
            Total = 1
        };

        _offerServiceMock.Setup(s => s.GetOffersAsync(It.IsAny<GetOffersRequest>()))
            .ReturnsAsync(expectedResponse);

        var result = await _controller.GetOffers(request);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<PaginatedResponse<OfferResponse>>(okResult.Value);
        Assert.Equal(expectedResponse, payload);
    }

    [Fact]
    public async Task GetOffers_WithNullRequest_ReturnsOkWithDefaultFilters()
    {
        var expectedResponse = new PaginatedResponse<OfferResponse>
        {
            Items = [],
            Page = 1,
            PageSize = 10,
            Total = 0
        };

        _offerServiceMock.Setup(s => s.GetOffersAsync(It.IsAny<GetOffersRequest>()))
            .ReturnsAsync(expectedResponse);

        var result = await _controller.GetOffers(null);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public async Task GetOffers_OnException_ReturnsServerError()
    {
        var request = new GetOffersRequest();
        _offerServiceMock.Setup(s => s.GetOffersAsync(It.IsAny<GetOffersRequest>()))
            .ThrowsAsync(new Exception("Database error"));

        var result = await _controller.GetOffers(request);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, objectResult.StatusCode);
        Assert.Equal("An error occurred while retrieving offers", GetMessage(objectResult.Value));
    }

    [Fact]
    public async Task GetOfferById_WithValidId_ReturnsOk()
    {
        var offerId = Guid.NewGuid();
        var expectedResponse = new OfferDetailResponse
        {
            Id = offerId,
            UserId = _testUser.Id,
            SkillId = 1,
            SkillName = "C# Programming",
            SkillCategoryCode = "TECH",
            Title = "Test Offer",
            Description = "Test Description",
            StatusCode = "Active",
            StatusLabel = "Active",
            Owner = new OfferOwnerInfo
            {
                Id = _testUser.Id,
                Name = _testUser.Name,
                Rating = 4.5m
            }
        };

        _offerServiceMock.Setup(s => s.GetOfferByIdAsync(offerId))
            .ReturnsAsync(expectedResponse);

        var result = await _controller.GetOfferById(offerId);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(expectedResponse, okResult.Value);
    }

    [Fact]
    public async Task GetOfferById_WithEmptyGuid_ReturnsBadRequest()
    {
        var result = await _controller.GetOfferById(Guid.Empty);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Invalid offer ID", GetMessage(badRequest.Value));
    }

    [Fact]
    public async Task GetOfferById_NotFound_ReturnsNotFound()
    {
        var offerId = Guid.NewGuid();
        _offerServiceMock.Setup(s => s.GetOfferByIdAsync(offerId))
            .ReturnsAsync((OfferDetailResponse?)null);

        var result = await _controller.GetOfferById(offerId);

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal("Offer not found", GetMessage(notFound.Value));
    }

    [Fact]
    public async Task GetOfferById_OnException_ReturnsServerError()
    {
        var offerId = Guid.NewGuid();
        _offerServiceMock.Setup(s => s.GetOfferByIdAsync(offerId))
            .ThrowsAsync(new Exception("Database error"));

        var result = await _controller.GetOfferById(offerId);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, objectResult.StatusCode);
        Assert.Equal("An error occurred while retrieving the offer", GetMessage(objectResult.Value));
    }



    [Fact]
    public async Task CreateOffer_InvalidModelState_ReturnsBadRequest()
    {
        _controller.ModelState.AddModelError("Title", "Required");
        var request = new CreateOfferRequest { Title = "", SkillId = 1 };

        var result = await _controller.CreateOffer(request);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Invalid request", GetMessage(badRequest.Value));
        _offerServiceMock.Verify(s => s.CreateOfferAsync(It.IsAny<Guid>(), It.IsAny<CreateOfferRequest>()), Times.Never);
    }

    [Fact]
    public async Task CreateOffer_UserNotFound_ReturnsUnauthorized()
    {
        var request = new CreateOfferRequest { Title = "Test", SkillId = 1 };
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync((ApplicationUser?)null);

        var result = await _controller.CreateOffer(request);

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal("User not found", GetMessage(unauthorized.Value));
    }

    [Fact]
    public async Task CreateOffer_ServiceReturnsNull_ReturnsBadRequest()
    {
        var request = new CreateOfferRequest { Title = "Test", SkillId = 1 };
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(_testUser);
        _offerServiceMock.Setup(s => s.CreateOfferAsync(_testUser.Id, request))
            .ReturnsAsync((OfferResponse?)null);

        var result = await _controller.CreateOffer(request);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Failed to create offer. Please check your input and try again.", GetMessage(badRequest.Value));
    }

    [Fact]
    public async Task CreateOffer_Success_ReturnsCreatedAtAction()
    {
        var request = new CreateOfferRequest { Title = "Test Offer", Description = "Description", SkillId = 1 };
        var response = new OfferResponse
        {
            Id = Guid.NewGuid(),
            UserId = _testUser.Id,
            SkillId = 1,
            Title = "Test Offer",
            Description = "Description",
            StatusCode = "Active",
            StatusLabel = "Active"
        };

        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(_testUser);
        _offerServiceMock.Setup(s => s.CreateOfferAsync(_testUser.Id, request))
            .ReturnsAsync(response);

        var result = await _controller.CreateOffer(request);

        var createdAt = Assert.IsType<CreatedAtActionResult>(result);
        Assert.Equal(nameof(OffersController.GetOfferById), createdAt.ActionName);
        Assert.Equal(response.Id, createdAt.RouteValues?["id"]);
        Assert.Equal(response, createdAt.Value);
    }

    [Fact]
    public async Task CreateOffer_OnException_ReturnsServerError()
    {
        var request = new CreateOfferRequest { Title = "Test", SkillId = 1 };
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(_testUser);
        _offerServiceMock.Setup(s => s.CreateOfferAsync(_testUser.Id, request))
            .ThrowsAsync(new Exception("Database error"));

        var result = await _controller.CreateOffer(request);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, objectResult.StatusCode);
        Assert.Equal("An error occurred while creating the offer", GetMessage(objectResult.Value));
    }


    [Fact]
    public async Task UpdateOffer_WithEmptyGuid_ReturnsBadRequest()
    {
        var request = new UpdateOfferRequest { Title = "Updated" };

        var result = await _controller.UpdateOffer(Guid.Empty, request);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Invalid offer ID", GetMessage(badRequest.Value));
    }

    [Fact]
    public async Task UpdateOffer_InvalidModelState_ReturnsBadRequest()
    {
        _controller.ModelState.AddModelError("Title", "Invalid");
        var offerId = Guid.NewGuid();
        var request = new UpdateOfferRequest { Title = "" };

        var result = await _controller.UpdateOffer(offerId, request);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Invalid request", GetMessage(badRequest.Value));
    }

    [Fact]
    public async Task UpdateOffer_UserNotFound_ReturnsUnauthorized()
    {
        var offerId = Guid.NewGuid();
        var request = new UpdateOfferRequest { Title = "Updated" };
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync((ApplicationUser?)null);

        var result = await _controller.UpdateOffer(offerId, request);

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal("User not found", GetMessage(unauthorized.Value));
    }

    [Fact]
    public async Task UpdateOffer_OfferNotFoundOrUnauthorized_ReturnsNotFound()
    {
        var offerId = Guid.NewGuid();
        var request = new UpdateOfferRequest { Title = "Updated" };

        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(_testUser);
        _userManagerMock.Setup(u => u.IsInRoleAsync(_testUser, Constants.AppRoles.Admin))
            .ReturnsAsync(false);
        _offerServiceMock.Setup(s => s.UpdateOfferAsync(offerId, _testUser.Id, request, false))
            .ReturnsAsync((OfferResponse?)null);

        var result = await _controller.UpdateOffer(offerId, request);

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal("Offer not found or you are not authorized to update it", GetMessage(notFound.Value));
    }

    [Fact]
    public async Task UpdateOffer_Success_ReturnsOk()
    {
        var offerId = Guid.NewGuid();
        var request = new UpdateOfferRequest { Title = "Updated Title" };
        var response = new OfferResponse
        {
            Id = offerId,
            UserId = _testUser.Id,
            SkillId = 1,
            Title = "Updated Title",
            StatusCode = "Active",
            StatusLabel = "Active"
        };

        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(_testUser);
        _userManagerMock.Setup(u => u.IsInRoleAsync(_testUser, Constants.AppRoles.Admin))
            .ReturnsAsync(false);
        _offerServiceMock.Setup(s => s.UpdateOfferAsync(offerId, _testUser.Id, request, false))
            .ReturnsAsync(response);

        var result = await _controller.UpdateOffer(offerId, request);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(response, okResult.Value);
    }

    [Fact]
    public async Task UpdateOffer_AsAdmin_PassesAdminFlag()
    {
        var offerId = Guid.NewGuid();
        var request = new UpdateOfferRequest { Title = "Updated" };
        var response = new OfferResponse
        {
            Id = offerId,
            UserId = Guid.NewGuid(),
            SkillId = 1,
            Title = "Updated",
            StatusCode = "Active"
        };

        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(_testUser);
        _userManagerMock.Setup(u => u.IsInRoleAsync(_testUser, Constants.AppRoles.Admin))
            .ReturnsAsync(true);
        _offerServiceMock.Setup(s => s.UpdateOfferAsync(offerId, _testUser.Id, request, true))
            .ReturnsAsync(response);

        var result = await _controller.UpdateOffer(offerId, request);

        Assert.IsType<OkObjectResult>(result);
        _offerServiceMock.Verify(s => s.UpdateOfferAsync(offerId, _testUser.Id, request, true), Times.Once);
    }

    [Fact]
    public async Task UpdateOffer_OnException_ReturnsServerError()
    {
        var offerId = Guid.NewGuid();
        var request = new UpdateOfferRequest { Title = "Updated" };

        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(_testUser);
        _userManagerMock.Setup(u => u.IsInRoleAsync(_testUser, Constants.AppRoles.Admin))
            .ReturnsAsync(false);
        _offerServiceMock.Setup(s => s.UpdateOfferAsync(offerId, _testUser.Id, request, false))
            .ThrowsAsync(new Exception("Database error"));

        var result = await _controller.UpdateOffer(offerId, request);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, objectResult.StatusCode);
        Assert.Equal("An error occurred while updating the offer", GetMessage(objectResult.Value));
    }


    [Fact]
    public async Task DeleteOffer_WithEmptyGuid_ReturnsBadRequest()
    {
        var result = await _controller.DeleteOffer(Guid.Empty);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Invalid offer ID", GetMessage(badRequest.Value));
    }

    [Fact]
    public async Task DeleteOffer_UserNotFound_ReturnsUnauthorized()
    {
        var offerId = Guid.NewGuid();
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync((ApplicationUser?)null);

        var result = await _controller.DeleteOffer(offerId);

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal("User not found", GetMessage(unauthorized.Value));
    }

    [Fact]
    public async Task DeleteOffer_OfferNotFoundOrUnauthorized_ReturnsNotFound()
    {
        var offerId = Guid.NewGuid();

        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(_testUser);
        _userManagerMock.Setup(u => u.IsInRoleAsync(_testUser, Constants.AppRoles.Admin))
            .ReturnsAsync(false);
        _offerServiceMock.Setup(s => s.DeleteOfferAsync(offerId, _testUser.Id, false))
            .ReturnsAsync(false);

        var result = await _controller.DeleteOffer(offerId);

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal("Offer not found or you are not authorized to delete it", GetMessage(notFound.Value));
    }

    [Fact]
    public async Task DeleteOffer_Success_ReturnsOkWithMessage()
    {
        var offerId = Guid.NewGuid();

        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(_testUser);
        _userManagerMock.Setup(u => u.IsInRoleAsync(_testUser, Constants.AppRoles.Admin))
            .ReturnsAsync(false);
        _offerServiceMock.Setup(s => s.DeleteOfferAsync(offerId, _testUser.Id, false))
            .ReturnsAsync(true);

        var result = await _controller.DeleteOffer(offerId);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal("Offer successfully deleted", GetMessage(okResult.Value));
    }

    [Fact]
    public async Task DeleteOffer_AsAdmin_PassesAdminFlag()
    {
        var offerId = Guid.NewGuid();

        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(_testUser);
        _userManagerMock.Setup(u => u.IsInRoleAsync(_testUser, Constants.AppRoles.Admin))
            .ReturnsAsync(true);
        _offerServiceMock.Setup(s => s.DeleteOfferAsync(offerId, _testUser.Id, true))
            .ReturnsAsync(true);

        var result = await _controller.DeleteOffer(offerId);

        Assert.IsType<OkObjectResult>(result);
        _offerServiceMock.Verify(s => s.DeleteOfferAsync(offerId, _testUser.Id, true), Times.Once);
    }

    [Fact]
    public async Task DeleteOffer_OnException_ReturnsServerError()
    {
        var offerId = Guid.NewGuid();

        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(_testUser);
        _userManagerMock.Setup(u => u.IsInRoleAsync(_testUser, Constants.AppRoles.Admin))
            .ReturnsAsync(false);
        _offerServiceMock.Setup(s => s.DeleteOfferAsync(offerId, _testUser.Id, false))
            .ThrowsAsync(new Exception("Database error"));

        var result = await _controller.DeleteOffer(offerId);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, objectResult.StatusCode);
        Assert.Equal("An error occurred while deleting the offer", GetMessage(objectResult.Value));
    }

    private static string? GetMessage(object? value) =>
        value?.GetType().GetProperty("message")?.GetValue(value)?.ToString();
}
