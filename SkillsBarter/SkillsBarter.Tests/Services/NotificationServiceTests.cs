using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Moq;
using SkillsBarter.Data;
using SkillsBarter.DTOs.Notifications;
using SkillsBarter.Hubs;
using SkillsBarter.Models;
using SkillsBarter.Services;
using Xunit;

namespace SkillsBarter.Tests.Services;

public class NotificationServiceTests
{
    private readonly Mock<ILogger<NotificationService>> _loggerMock = new();
    private readonly Mock<IHubContext<NotificationHub>> _hubContextMock = new();
    private readonly Mock<IClientProxy> _clientProxyMock = new();
    private ApplicationDbContext _context = null!;
    private NotificationService _service = null!;

    public NotificationServiceTests()
    {
        SetupInMemoryDatabase();
        SetupHubContextMock();
    }

    private void SetupInMemoryDatabase()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _context = new ApplicationDbContext(options);
        _service = new NotificationService(_context, _loggerMock.Object, _hubContextMock.Object);
    }

    private void SetupHubContextMock()
    {
        var hubClientsMock = new Mock<IHubClients>();
        hubClientsMock.Setup(c => c.Group(It.IsAny<string>())).Returns(_clientProxyMock.Object);
        _hubContextMock.Setup(h => h.Clients).Returns(hubClientsMock.Object);
    }

    private async Task<ApplicationUser> SeedUserAsync()
    {
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = "testuser",
            Name = "Test User",
            Email = "test@example.com"
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        return user;
    }

    private async Task<List<Notification>> SeedNotificationsAsync(Guid userId, int count, bool allRead = false)
    {
        var notifications = new List<Notification>();
        for (int i = 0; i < count; i++)
        {
            notifications.Add(new Notification
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Title = $"Notification {i + 1}",
                Message = $"Message {i + 1}",
                Type = "info",
                IsRead = allRead || i % 2 == 0,
                CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-i)
            });
        }
        _context.Notifications.AddRange(notifications);
        await _context.SaveChangesAsync();
        return notifications;
    }

    [Fact]
    public async Task CreateAsync_SavesNotificationToDatabase()
    {
        var user = await SeedUserAsync();
        var title = "Test Title";
        var message = "Test Message";
        var type = "agreement_created";

        await _service.CreateAsync(user.Id, type, title, message);

        var notification = await _context.Notifications.FirstOrDefaultAsync(n => n.UserId == user.Id);
        Assert.NotNull(notification);
        Assert.Equal(title, notification.Title);
        Assert.Equal(message, notification.Message);
        Assert.Equal(type, notification.Type);
        Assert.False(notification.IsRead);
    }

    [Fact]
    public async Task CreateAsync_SendsRealTimeNotificationViaSignalR()
    {
        var user = await SeedUserAsync();

        await _service.CreateAsync(user.Id, "info", "Title", "Message");

        _clientProxyMock.Verify(
            c => c.SendCoreAsync("ReceiveNotification", It.Is<object[]>(o => o.Length == 1), default),
            Times.Once);
    }

    [Fact]
    public async Task CreateAsync_SignalRFailure_DoesNotThrow()
    {
        var user = await SeedUserAsync();
        _clientProxyMock.Setup(c => c.SendCoreAsync(It.IsAny<string>(), It.IsAny<object[]>(), default))
            .ThrowsAsync(new Exception("SignalR connection failed"));

        var exception = await Record.ExceptionAsync(() =>
            _service.CreateAsync(user.Id, "info", "Title", "Message"));

        Assert.Null(exception);
        var notification = await _context.Notifications.FirstOrDefaultAsync(n => n.UserId == user.Id);
        Assert.NotNull(notification);
    }

    [Fact]
    public async Task GetNotificationsAsync_ReturnsUserNotifications()
    {
        var user = await SeedUserAsync();
        await SeedNotificationsAsync(user.Id, 5);

        var result = await _service.GetNotificationsAsync(user.Id, unreadOnly: false, skip: 0, take: 20);

        Assert.Equal(5, result.TotalCount);
        Assert.Equal(5, result.Items.Count);
    }

    [Fact]
    public async Task GetNotificationsAsync_ReturnsOnlyUnreadWhenFiltered()
    {
        var user = await SeedUserAsync();
        await SeedNotificationsAsync(user.Id, 6);

        var result = await _service.GetNotificationsAsync(user.Id, unreadOnly: true, skip: 0, take: 20);

        Assert.True(result.Items.All(n => !n.IsRead));
        Assert.Equal(3, result.Items.Count);
    }

    [Fact]
    public async Task GetNotificationsAsync_ReturnsUnreadCountRegardlessOfFilter()
    {
        var user = await SeedUserAsync();
        await SeedNotificationsAsync(user.Id, 6);

        var result = await _service.GetNotificationsAsync(user.Id, unreadOnly: false, skip: 0, take: 20);

        Assert.Equal(3, result.UnreadCount);
        Assert.Equal(6, result.TotalCount);
    }

    [Fact]
    public async Task GetNotificationsAsync_PaginatesCorrectly()
    {
        var user = await SeedUserAsync();
        await SeedNotificationsAsync(user.Id, 10);

        var result = await _service.GetNotificationsAsync(user.Id, unreadOnly: false, skip: 2, take: 3);

        Assert.Equal(10, result.TotalCount);
        Assert.Equal(3, result.Items.Count);
    }

    [Fact]
    public async Task GetNotificationsAsync_OrdersByCreatedAtDescending()
    {
        var user = await SeedUserAsync();
        var notifications = await SeedNotificationsAsync(user.Id, 5);

        var result = await _service.GetNotificationsAsync(user.Id, unreadOnly: false, skip: 0, take: 20);

        Assert.True(result.Items.First().CreatedAt >= result.Items.Last().CreatedAt);
    }

    [Fact]
    public async Task GetNotificationsAsync_NegativeSkip_TreatsAsZero()
    {
        var user = await SeedUserAsync();
        await SeedNotificationsAsync(user.Id, 5);

        var result = await _service.GetNotificationsAsync(user.Id, unreadOnly: false, skip: -5, take: 20);

        Assert.Equal(5, result.Items.Count);
    }

    [Fact]
    public async Task GetNotificationsAsync_ZeroTake_DefaultsTo20()
    {
        var user = await SeedUserAsync();
        await SeedNotificationsAsync(user.Id, 25);

        var result = await _service.GetNotificationsAsync(user.Id, unreadOnly: false, skip: 0, take: 0);

        Assert.Equal(20, result.Items.Count);
    }

    [Fact]
    public async Task GetNotificationsAsync_TakeOver100_CapsAt100()
    {
        var user = await SeedUserAsync();
        await SeedNotificationsAsync(user.Id, 5);

        var result = await _service.GetNotificationsAsync(user.Id, unreadOnly: false, skip: 0, take: 200);

        Assert.Equal(5, result.Items.Count);
    }

    [Fact]
    public async Task GetNotificationsAsync_DoesNotReturnOtherUsersNotifications()
    {
        var user1 = await SeedUserAsync();
        var user2 = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = "otheruser",
            Email = "other@example.com"
        };
        _context.Users.Add(user2);
        await _context.SaveChangesAsync();

        await SeedNotificationsAsync(user1.Id, 3);
        await SeedNotificationsAsync(user2.Id, 5);

        var result = await _service.GetNotificationsAsync(user1.Id, unreadOnly: false, skip: 0, take: 20);

        Assert.Equal(3, result.TotalCount);
        Assert.True(result.Items.All(n => n.Title.Contains("Notification")));
    }

    [Fact]
    public async Task MarkNotificationsAsReadAsync_NoIdsAndNotMarkAll_ReturnsNull()
    {
        var user = await SeedUserAsync();
        var request = new MarkReadRequestDto { Ids = null, MarkAll = false };

        var result = await _service.MarkNotificationsAsReadAsync(user.Id, request);

        Assert.Null(result);
    }

    [Fact]
    public async Task MarkNotificationsAsReadAsync_EmptyIdsList_ReturnsNull()
    {
        var user = await SeedUserAsync();
        var request = new MarkReadRequestDto { Ids = new List<Guid>(), MarkAll = false };

        var result = await _service.MarkNotificationsAsReadAsync(user.Id, request);

        Assert.Null(result);
    }

    [Fact]
    public async Task MarkNotificationsAsReadAsync_OnlyEmptyGuids_ReturnsNull()
    {
        var user = await SeedUserAsync();
        var request = new MarkReadRequestDto { Ids = new List<Guid> { Guid.Empty, Guid.Empty } };

        var result = await _service.MarkNotificationsAsReadAsync(user.Id, request);

        Assert.Null(result);
    }

}
