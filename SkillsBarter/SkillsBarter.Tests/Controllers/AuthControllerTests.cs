using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using SkillsBarter.Controllers;
using SkillsBarter.Data;
using SkillsBarter.DTOs;
using SkillsBarter.Models;
using SkillsBarter.Services;
using SkillsBarter.Tests.TestUtils;
using Xunit;

namespace SkillsBarter.Tests.Controllers;

public class AuthControllerTests
{
    private readonly Mock<UserManager<ApplicationUser>> _userManagerMock;
    private readonly Mock<SignInManager<ApplicationUser>> _signInManagerMock;
    private readonly Mock<ITokenService> _tokenServiceMock = new();
    private readonly Mock<IUserService> _userServiceMock = new();
    private readonly Mock<IEmailService> _emailServiceMock = new();
    private readonly Mock<ILogger<AuthController>> _loggerMock = new();
    private readonly Mock<IConfiguration> _configurationMock = new();
    private readonly ApplicationDbContext _context;
    private readonly AuthController _controller;

    public AuthControllerTests()
    {
        _userManagerMock = IdentityMocks.CreateUserManager<ApplicationUser>();

        var contextAccessor = new Mock<IHttpContextAccessor>();
        var claimsFactory = new Mock<IUserClaimsPrincipalFactory<ApplicationUser>>();
        _signInManagerMock = new Mock<SignInManager<ApplicationUser>>(
            _userManagerMock.Object,
            contextAccessor.Object,
            claimsFactory.Object,
            null!, null!, null!, null!
        );

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        _context = new ApplicationDbContext(options);

        _controller = new AuthController(
            _userManagerMock.Object,
            _signInManagerMock.Object,
            _tokenServiceMock.Object,
            _userServiceMock.Object,
            _emailServiceMock.Object,
            _context,
            _loggerMock.Object,
            _configurationMock.Object
        );

        SetupControllerContext();
    }

    private void SetupControllerContext()
    {
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
    }

    [Fact]
    public async Task Register_InvalidModelState_ReturnsBadRequest()
    {
        _controller.ModelState.AddModelError("Email", "Required");

        var result = await _controller.Register(new RegisterRequest());

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var response = Assert.IsType<AuthResponse>(badRequest.Value);
        Assert.False(response.Success);
        Assert.Equal("Validation failed", response.Message);
    }

    [Fact]
    public async Task Register_UserAlreadyExists_ReturnsBadRequest()
    {
        var existingUser = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = "existing@example.com"
        };

        _userManagerMock.Setup(u => u.FindByEmailAsync("existing@example.com"))
            .ReturnsAsync(existingUser);

        var request = new RegisterRequest
        {
            Email = "existing@example.com",
            Password = "Password123!",
            ConfirmPassword = "Password123!",
            Name = "Test User"
        };

        var result = await _controller.Register(request);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var response = Assert.IsType<AuthResponse>(badRequest.Value);
        Assert.False(response.Success);
        Assert.Contains("already exists", response.Message);
    }

    [Fact]
    public async Task Register_CreateUserFails_ReturnsBadRequest()
    {
        _userManagerMock.Setup(u => u.FindByEmailAsync(It.IsAny<string>()))
            .ReturnsAsync((ApplicationUser?)null);

        _userManagerMock.Setup(u => u.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Password too weak" }));

        var request = new RegisterRequest
        {
            Email = "new@example.com",
            Password = "weak",
            ConfirmPassword = "weak",
            Name = "Test User"
        };

        var result = await _controller.Register(request);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var response = Assert.IsType<AuthResponse>(badRequest.Value);
        Assert.False(response.Success);
        Assert.Contains("Password too weak", response.Errors!);
    }

    [Fact]
    public async Task Register_Success_ReturnsOkWithToken()
    {
        _userManagerMock.Setup(u => u.FindByEmailAsync(It.IsAny<string>()))
            .ReturnsAsync((ApplicationUser?)null);

        _userManagerMock.Setup(u => u.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success);

        _userManagerMock.Setup(u => u.AddToRoleAsync(It.IsAny<ApplicationUser>(), "Freemium"))
            .ReturnsAsync(IdentityResult.Success);

        _userManagerMock.Setup(u => u.GetRolesAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync(new List<string> { "Freemium" });

        _tokenServiceMock.Setup(t => t.GenerateAccessToken(It.IsAny<ApplicationUser>(), It.IsAny<IList<string>>()))
            .Returns("test-jwt-token");

        var request = new RegisterRequest
        {
            Email = "new@example.com",
            Password = "Password123!",
            ConfirmPassword = "Password123!",
            Name = "Test User"
        };

        var result = await _controller.Register(request);

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<AuthResponse>(ok.Value);
        Assert.True(response.Success);
        Assert.Equal("test-jwt-token", response.Token);
        Assert.NotNull(response.User);

        _emailServiceMock.Verify(
            e => e.SendVerificationEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public async Task Register_WithSkills_AddsSkillsToUser()
    {
        var category = new SkillCategory { Code = "TECH", Label = "Technology" };
        var skill = new Skill { Id = 1, Name = "Programming", CategoryCode = category.Code, Category = category };
        _context.SkillCategories.Add(category);
        _context.Skills.Add(skill);
        await _context.SaveChangesAsync();

        _userManagerMock.Setup(u => u.FindByEmailAsync(It.IsAny<string>()))
            .ReturnsAsync((ApplicationUser?)null);

        _userManagerMock.Setup(u => u.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success);

        _userManagerMock.Setup(u => u.AddToRoleAsync(It.IsAny<ApplicationUser>(), "Freemium"))
            .ReturnsAsync(IdentityResult.Success);

        _userManagerMock.Setup(u => u.GetRolesAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync(new List<string> { "Freemium" });

        _tokenServiceMock.Setup(t => t.GenerateAccessToken(It.IsAny<ApplicationUser>(), It.IsAny<IList<string>>()))
            .Returns("test-jwt-token");

        var request = new RegisterRequest
        {
            Email = "new@example.com",
            Password = "Password123!",
            ConfirmPassword = "Password123!",
            Name = "Test User",
            SkillIds = new List<int> { 1 }
        };

        var result = await _controller.Register(request);

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<AuthResponse>(ok.Value);
        Assert.True(response.Success);

        var userSkills = await _context.UserSkills.ToListAsync();
        Assert.Single(userSkills);
    }

    [Fact]
    public async Task Login_InvalidModelState_ReturnsBadRequest()
    {
        _controller.ModelState.AddModelError("Email", "Required");

        var result = await _controller.Login(new LoginRequest());

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Login_EmptyCredentials_ReturnsBadRequest()
    {
        var request = new LoginRequest { Email = "", Password = "" };

        var result = await _controller.Login(request);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var response = Assert.IsType<AuthResponse>(badRequest.Value);
        Assert.False(response.Success);
    }

    [Fact]
    public async Task Login_UserNotFound_ReturnsUnauthorized()
    {
        _userManagerMock.Setup(u => u.FindByEmailAsync(It.IsAny<string>()))
            .ReturnsAsync((ApplicationUser?)null);

        var request = new LoginRequest { Email = "notfound@example.com", Password = "Password123!" };

        var result = await _controller.Login(request);

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result);
        var response = Assert.IsType<AuthResponse>(unauthorized.Value);
        Assert.False(response.Success);
        Assert.Equal("Invalid email or password", response.Message);
    }

    [Fact]
    public async Task Login_InvalidPassword_ReturnsUnauthorized()
    {
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            UserName = "test@example.com"
        };

        _userManagerMock.Setup(u => u.FindByEmailAsync("test@example.com"))
            .ReturnsAsync(user);

        _userManagerMock.Setup(u => u.CheckPasswordAsync(user, "WrongPassword"))
            .ReturnsAsync(false);

        var request = new LoginRequest { Email = "test@example.com", Password = "WrongPassword" };

        var result = await _controller.Login(request);

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result);
        var response = Assert.IsType<AuthResponse>(unauthorized.Value);
        Assert.False(response.Success);
    }

    [Fact]
    public async Task Login_Success_ReturnsOkWithToken()
    {
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            UserName = "test@example.com",
            Name = "Test User"
        };

        _userManagerMock.Setup(u => u.FindByEmailAsync("test@example.com"))
            .ReturnsAsync(user);

        _userManagerMock.Setup(u => u.CheckPasswordAsync(user, "Password123!"))
            .ReturnsAsync(true);

        _userManagerMock.Setup(u => u.GetRolesAsync(user))
            .ReturnsAsync(new List<string> { "Freemium" });

        _tokenServiceMock.Setup(t => t.GenerateAccessToken(user, It.IsAny<IList<string>>()))
            .Returns("test-jwt-token");

        var request = new LoginRequest { Email = "test@example.com", Password = "Password123!" };

        var result = await _controller.Login(request);

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<AuthResponse>(ok.Value);
        Assert.True(response.Success);
        Assert.Equal("test-jwt-token", response.Token);
    }

    [Fact]
    public void Logout_ReturnsOk()
    {
        var result = _controller.Logout();

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Contains("logged out", ok.Value?.GetType().GetProperty("message")?.GetValue(ok.Value)?.ToString());
    }

    [Fact]
    public async Task GetCurrentUserProfile_UserNotFound_ReturnsUnauthorized()
    {
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync((ApplicationUser?)null);

        var result = await _controller.GetCurrentUserProfile();

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task GetCurrentUserProfile_ProfileNotFound_ReturnsServerError()
    {
        var user = new ApplicationUser { Id = Guid.NewGuid() };
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(user);

        _userServiceMock.Setup(s => s.GetDetailedProfileAsync(user.Id))
            .ReturnsAsync((DetailedUserProfileResponse?)null);

        var result = await _controller.GetCurrentUserProfile();

        var statusCode = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusCode.StatusCode);
    }

    [Fact]
    public async Task GetCurrentUserProfile_Success_ReturnsOk()
    {
        var user = new ApplicationUser { Id = Guid.NewGuid() };
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(user);

        var profile = new DetailedUserProfileResponse { Id = user.Id, Name = "Test User" };
        _userServiceMock.Setup(s => s.GetDetailedProfileAsync(user.Id))
            .ReturnsAsync(profile);

        var result = await _controller.GetCurrentUserProfile();

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(ok.Value);
    }

    [Fact]
    public async Task VerifyEmail_EmptyToken_ReturnsBadRequest()
    {
        var result = await _controller.VerifyEmail("");

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("Token is required", badRequest.Value?.GetType().GetProperty("message")?.GetValue(badRequest.Value)?.ToString());
    }

    [Fact]
    public async Task VerifyEmail_InvalidToken_ReturnsBadRequest()
    {
        _userManagerMock.Setup(u => u.Users)
            .Returns(new List<ApplicationUser>().AsQueryable().BuildMockDbSet().Object);

        var result = await _controller.VerifyEmail("invalid-token");

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("Invalid verification token", badRequest.Value?.GetType().GetProperty("message")?.GetValue(badRequest.Value)?.ToString());
    }

    [Fact]
    public async Task VerifyEmail_ExpiredToken_ReturnsBadRequest()
    {
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            EmailVerificationToken = "valid-token",
            EmailVerificationTokenExpiry = DateTime.UtcNow.AddHours(-1)
        };

        var users = new List<ApplicationUser> { user }.AsQueryable();
        _userManagerMock.Setup(u => u.Users).Returns(users.BuildMockDbSet().Object);

        var result = await _controller.VerifyEmail("valid-token");

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("expired", badRequest.Value?.GetType().GetProperty("message")?.GetValue(badRequest.Value)?.ToString());
    }

    [Fact]
    public async Task VerifyEmail_AlreadyVerified_ReturnsOk()
    {
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            EmailVerificationToken = "valid-token",
            EmailVerificationTokenExpiry = DateTime.UtcNow.AddHours(24),
            EmailConfirmed = true
        };

        var users = new List<ApplicationUser> { user }.AsQueryable();
        _userManagerMock.Setup(u => u.Users).Returns(users.BuildMockDbSet().Object);

        var result = await _controller.VerifyEmail("valid-token");

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Contains("already verified", ok.Value?.GetType().GetProperty("message")?.GetValue(ok.Value)?.ToString());
    }

    [Fact]
    public async Task VerifyEmail_Success_ReturnsOk()
    {
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            EmailVerificationToken = "valid-token",
            EmailVerificationTokenExpiry = DateTime.UtcNow.AddHours(24),
            EmailConfirmed = false
        };

        var users = new List<ApplicationUser> { user }.AsQueryable();
        _userManagerMock.Setup(u => u.Users).Returns(users.BuildMockDbSet().Object);
        _userManagerMock.Setup(u => u.UpdateAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync(IdentityResult.Success);

        var result = await _controller.VerifyEmail("valid-token");

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Contains("verified successfully", ok.Value?.GetType().GetProperty("message")?.GetValue(ok.Value)?.ToString());
    }

    [Fact]
    public async Task ForgotPassword_InvalidModelState_ReturnsBadRequest()
    {
        _controller.ModelState.AddModelError("Email", "Required");

        var result = await _controller.ForgotPassword(new ForgotPasswordRequest());

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task ForgotPassword_UserNotFound_ReturnsOkWithGenericMessage()
    {
        _userManagerMock.Setup(u => u.FindByEmailAsync(It.IsAny<string>()))
            .ReturnsAsync((ApplicationUser?)null);

        var result = await _controller.ForgotPassword(new ForgotPasswordRequest { Email = "notfound@example.com" });

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Contains("If an account", ok.Value?.GetType().GetProperty("message")?.GetValue(ok.Value)?.ToString());
    }

    [Fact]
    public async Task ForgotPassword_Success_ReturnsOkAndSendsEmail()
    {
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            Name = "Test User"
        };

        _userManagerMock.Setup(u => u.FindByEmailAsync("test@example.com"))
            .ReturnsAsync(user);

        _userManagerMock.Setup(u => u.UpdateAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync(IdentityResult.Success);

        var result = await _controller.ForgotPassword(new ForgotPasswordRequest { Email = "test@example.com" });

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Contains("If an account", ok.Value?.GetType().GetProperty("message")?.GetValue(ok.Value)?.ToString());

        _emailServiceMock.Verify(
            e => e.SendPasswordResetEmailAsync(user.Email, user.Name, It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public async Task ResetPassword_InvalidModelState_ReturnsBadRequest()
    {
        _controller.ModelState.AddModelError("NewPassword", "Required");

        var result = await _controller.ResetPassword(new ResetPasswordRequest());

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task ResetPassword_InvalidToken_ReturnsBadRequest()
    {
        _userManagerMock.Setup(u => u.Users)
            .Returns(new List<ApplicationUser>().AsQueryable().BuildMockDbSet().Object);

        var result = await _controller.ResetPassword(new ResetPasswordRequest
        {
            Token = "invalid",
            NewPassword = "NewPassword123!",
            ConfirmPassword = "NewPassword123!"
        });

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("Invalid or expired", badRequest.Value?.GetType().GetProperty("message")?.GetValue(badRequest.Value)?.ToString());
    }

    [Fact]
    public async Task ResetPassword_ExpiredToken_ReturnsBadRequest()
    {
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            PasswordResetToken = "valid-token",
            PasswordResetTokenExpiry = DateTime.UtcNow.AddHours(-1)
        };

        var users = new List<ApplicationUser> { user }.AsQueryable();
        _userManagerMock.Setup(u => u.Users).Returns(users.BuildMockDbSet().Object);

        var result = await _controller.ResetPassword(new ResetPasswordRequest
        {
            Token = "valid-token",
            NewPassword = "NewPassword123!",
            ConfirmPassword = "NewPassword123!"
        });

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("Invalid or expired", badRequest.Value?.GetType().GetProperty("message")?.GetValue(badRequest.Value)?.ToString());
    }

    [Fact]
    public async Task ResetPassword_Success_ReturnsOk()
    {
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            PasswordResetToken = "valid-token",
            PasswordResetTokenExpiry = DateTime.UtcNow.AddHours(1)
        };

        var users = new List<ApplicationUser> { user }.AsQueryable();
        _userManagerMock.Setup(u => u.Users).Returns(users.BuildMockDbSet().Object);
        _userManagerMock.Setup(u => u.GeneratePasswordResetTokenAsync(user))
            .ReturnsAsync("identity-reset-token");
        _userManagerMock.Setup(u => u.ResetPasswordAsync(user, "identity-reset-token", "NewPassword123!"))
            .ReturnsAsync(IdentityResult.Success);
        _userManagerMock.Setup(u => u.UpdateAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync(IdentityResult.Success);

        var result = await _controller.ResetPassword(new ResetPasswordRequest
        {
            Token = "valid-token",
            NewPassword = "NewPassword123!",
            ConfirmPassword = "NewPassword123!"
        });

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Contains("reset successfully", ok.Value?.GetType().GetProperty("message")?.GetValue(ok.Value)?.ToString());
    }

    [Fact]
    public void LoginGoogle_ReturnsChallengeResult()
    {
        var urlHelperMock = new Mock<IUrlHelper>();
        urlHelperMock.Setup(u => u.Action(It.IsAny<UrlActionContext>())).Returns("http://localhost/callback");
        _controller.Url = urlHelperMock.Object;

        var result = _controller.LoginGoogle();

        Assert.IsType<ChallengeResult>(result);
    }

    [Fact]
    public void LoginFacebook_ReturnsChallengeResult()
    {
        var urlHelperMock = new Mock<IUrlHelper>();
        urlHelperMock.Setup(u => u.Action(It.IsAny<UrlActionContext>())).Returns("http://localhost/callback");
        _controller.Url = urlHelperMock.Object;

        var result = _controller.LoginFacebook();

        Assert.IsType<ChallengeResult>(result);
    }
}

public static class MockDbSetExtensions
{
    public static Mock<DbSet<T>> BuildMockDbSet<T>(this IQueryable<T> source) where T : class
    {
        var mock = new Mock<DbSet<T>>();
        mock.As<IAsyncEnumerable<T>>()
            .Setup(m => m.GetAsyncEnumerator(It.IsAny<CancellationToken>()))
            .Returns(new TestAsyncEnumerator<T>(source.GetEnumerator()));
        mock.As<IQueryable<T>>().Setup(m => m.Provider).Returns(new TestAsyncQueryProvider<T>(source.Provider));
        mock.As<IQueryable<T>>().Setup(m => m.Expression).Returns(source.Expression);
        mock.As<IQueryable<T>>().Setup(m => m.ElementType).Returns(source.ElementType);
        mock.As<IQueryable<T>>().Setup(m => m.GetEnumerator()).Returns(source.GetEnumerator());
        return mock;
    }
}

internal class TestAsyncQueryProvider<TEntity> : IAsyncQueryProvider
{
    private readonly IQueryProvider _inner;

    internal TestAsyncQueryProvider(IQueryProvider inner) => _inner = inner;

    public IQueryable CreateQuery(System.Linq.Expressions.Expression expression)
        => new TestAsyncEnumerable<TEntity>(expression);

    public IQueryable<TElement> CreateQuery<TElement>(System.Linq.Expressions.Expression expression)
        => new TestAsyncEnumerable<TElement>(expression);

    public object? Execute(System.Linq.Expressions.Expression expression)
        => _inner.Execute(expression);

    public TResult Execute<TResult>(System.Linq.Expressions.Expression expression)
        => _inner.Execute<TResult>(expression);

    public TResult ExecuteAsync<TResult>(System.Linq.Expressions.Expression expression, CancellationToken cancellationToken = default)
    {
        var resultType = typeof(TResult).GetGenericArguments()[0];
        var executionResult = typeof(IQueryProvider)
            .GetMethod(name: nameof(IQueryProvider.Execute), genericParameterCount: 1, types: new[] { typeof(System.Linq.Expressions.Expression) })
            ?.MakeGenericMethod(resultType)
            .Invoke(this, new object[] { expression });

        return (TResult)typeof(Task).GetMethod(nameof(Task.FromResult))
            ?.MakeGenericMethod(resultType)
            .Invoke(null, new[] { executionResult })!;
    }
}

internal class TestAsyncEnumerable<T> : EnumerableQuery<T>, IAsyncEnumerable<T>, IQueryable<T>
{
    public TestAsyncEnumerable(IEnumerable<T> enumerable) : base(enumerable) { }
    public TestAsyncEnumerable(System.Linq.Expressions.Expression expression) : base(expression) { }

    public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        => new TestAsyncEnumerator<T>(this.AsEnumerable().GetEnumerator());

    IQueryProvider IQueryable.Provider => new TestAsyncQueryProvider<T>(this);
}

internal class TestAsyncEnumerator<T> : IAsyncEnumerator<T>
{
    private readonly IEnumerator<T> _inner;

    public TestAsyncEnumerator(IEnumerator<T> inner) => _inner = inner;

    public ValueTask DisposeAsync()
    {
        _inner.Dispose();
        return ValueTask.CompletedTask;
    }

    public ValueTask<bool> MoveNextAsync() => ValueTask.FromResult(_inner.MoveNext());

    public T Current => _inner.Current;
}
