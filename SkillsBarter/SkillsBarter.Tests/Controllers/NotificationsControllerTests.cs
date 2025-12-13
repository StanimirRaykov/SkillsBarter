using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using SkillsBarter.Controllers;
using SkillsBarter.DTOs.Notifications;
using SkillsBarter.Services;
using Xunit;

namespace SkillsBarter.Tests.Controllers;

public class NotificationsControllerTests
{
    private readonly Mock<INotificationService> _notificationServiceMock = new();
    private readonly Mock<ILogger<NotificationsController>> _loggerMock = new();
    private readonly NotificationsController _controller;
    private readonly Guid _userId = Guid.NewGuid();

    public NotificationsControllerTests()
    {
        _controller = new NotificationsController(
            _notificationServiceMock.Object,
            _loggerMock.Object
        );
        SetupControllerContext(_userId);
    }

    private void SetupControllerContext(Guid? userId)
    {
        var claims = new List<Claim>();
        if (userId.HasValue)
        {
            claims.Add(new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString()));
        }
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var claimsPrincipal = new ClaimsPrincipal(identity);

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = claimsPrincipal }
        };
    }

    [Fact]
    public async Task GetNotifications_MissingUserContext_ReturnsUnauthorized()
    {
        SetupControllerContext(null);

        var result = await _controller.GetNotifications();

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result.Result);
        Assert.Equal("User context missing", GetMessage(unauthorized.Value));
    }

    [Fact]
    public async Task GetNotifications_InvalidUserIdFormat_ReturnsUnauthorized()
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, "not-a-guid")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };

        var result = await _controller.GetNotifications();

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result.Result);
        Assert.Equal("User context missing", GetMessage(unauthorized.Value));
    }

    [Fact]
    public async Task GetNotifications_Success_ReturnsNotificationsList()
    {
        var expectedResponse = new NotificationsListResponseDto
        {
            Items = new List<NotificationItemDto>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    Title = "Test Notification",
                    Message = "Test message",
                    Type = "info",
                    IsRead = false,
                    CreatedAt = DateTimeOffset.UtcNow
                }
            },
            TotalCount = 1,
            UnreadCount = 1
        };

        _notificationServiceMock.Setup(s => s.GetNotificationsAsync(_userId, false, 0, 20))
            .ReturnsAsync(expectedResponse);

        var result = await _controller.GetNotifications();

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<NotificationsListResponseDto>(okResult.Value);
        Assert.Equal(1, response.TotalCount);
        Assert.Single(response.Items);
    }

    [Fact]
    public async Task GetNotifications_WithUnreadOnlyFilter_PassesFilterToService()
    {
        var expectedResponse = new NotificationsListResponseDto
        {
            Items = new List<NotificationItemDto>(),
            TotalCount = 0,
            UnreadCount = 0
        };

        _notificationServiceMock.Setup(s => s.GetNotificationsAsync(_userId, true, 5, 10))
            .ReturnsAsync(expectedResponse);

        var result = await _controller.GetNotifications(unreadOnly: true, skip: 5, take: 10);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        _notificationServiceMock.Verify(s => s.GetNotificationsAsync(_userId, true, 5, 10), Times.Once);
    }

    [Fact]
    public async Task GetNotifications_ServiceThrows_Returns500()
    {
        _notificationServiceMock.Setup(s => s.GetNotificationsAsync(_userId, false, 0, 20))
            .ThrowsAsync(new Exception("Database error"));

        var result = await _controller.GetNotifications();

        var statusResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(500, statusResult.StatusCode);
        Assert.Equal("An error occurred while retrieving notifications", GetMessage(statusResult.Value));
    }

    [Fact]
    public async Task MarkRead_NullRequest_ReturnsBadRequest()
    {
        var result = await _controller.MarkRead(null);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Request body is required", GetMessage(badRequest.Value));
    }

    [Fact]
    public async Task MarkRead_MissingUserContext_ReturnsUnauthorized()
    {
        SetupControllerContext(null);
        var request = new MarkReadRequestDto { MarkAll = true };

        var result = await _controller.MarkRead(request);

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal("User context missing", GetMessage(unauthorized.Value));
    }

    [Fact]
    public async Task MarkRead_ServiceReturnsNull_ReturnsBadRequest()
    {
        var request = new MarkReadRequestDto { Ids = null, MarkAll = false };

        _notificationServiceMock.Setup(s => s.MarkNotificationsAsReadAsync(_userId, request))
            .ReturnsAsync((int?)null);

        var result = await _controller.MarkRead(request);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Provide notification ids or set markAll to true", GetMessage(badRequest.Value));
    }

    [Fact]
    public async Task MarkRead_MarkAllSuccess_ReturnsOkWithCount()
    {
        var request = new MarkReadRequestDto { MarkAll = true };

        _notificationServiceMock.Setup(s => s.MarkNotificationsAsReadAsync(_userId, request))
            .ReturnsAsync(5);

        var result = await _controller.MarkRead(request);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var successValue = okResult.Value?.GetType().GetProperty("success")?.GetValue(okResult.Value);
        var updatedValue = okResult.Value?.GetType().GetProperty("updated")?.GetValue(okResult.Value);
        Assert.Equal(true, successValue);
        Assert.Equal(5, updatedValue);
    }

    [Fact]
    public async Task MarkRead_SpecificIds_ReturnsOkWithCount()
    {
        var notificationIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };
        var request = new MarkReadRequestDto { Ids = notificationIds };

        _notificationServiceMock.Setup(s => s.MarkNotificationsAsReadAsync(_userId, request))
            .ReturnsAsync(2);

        var result = await _controller.MarkRead(request);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var updatedValue = okResult.Value?.GetType().GetProperty("updated")?.GetValue(okResult.Value);
        Assert.Equal(2, updatedValue);
    }

    [Fact]
    public async Task MarkRead_ServiceThrows_Returns500()
    {
        var request = new MarkReadRequestDto { MarkAll = true };

        _notificationServiceMock.Setup(s => s.MarkNotificationsAsReadAsync(_userId, request))
            .ThrowsAsync(new Exception("Database error"));

        var result = await _controller.MarkRead(request);

        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusResult.StatusCode);
        Assert.Equal("An error occurred while updating notifications", GetMessage(statusResult.Value));
    }

    private static string? GetMessage(object? value) =>
        value?.GetType().GetProperty("message")?.GetValue(value)?.ToString();
}
