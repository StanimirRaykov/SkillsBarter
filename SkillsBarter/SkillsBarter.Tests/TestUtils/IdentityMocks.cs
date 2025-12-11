using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace SkillsBarter.Tests.TestUtils;

public static class IdentityMocks
{
    public static Mock<UserManager<TUser>> CreateUserManager<TUser>()
        where TUser : class
    {
        var store = new Mock<IUserStore<TUser>>();
        var options = new Mock<IOptions<IdentityOptions>>();
        options.Setup(o => o.Value).Returns(new IdentityOptions());

        return new Mock<UserManager<TUser>>(
            store.Object,
            options.Object,
            new Mock<IPasswordHasher<TUser>>().Object,
            Array.Empty<IUserValidator<TUser>>(),
            Array.Empty<IPasswordValidator<TUser>>(),
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            new Mock<IServiceProvider>().Object,
            new Mock<ILogger<UserManager<TUser>>>().Object
        );
    }

    public static Mock<RoleManager<TRole>> CreateRoleManager<TRole>()
        where TRole : class
    {
        var store = new Mock<IRoleStore<TRole>>();

        return new Mock<RoleManager<TRole>>(
            store.Object,
            Array.Empty<IRoleValidator<TRole>>(),
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            new Mock<ILogger<RoleManager<TRole>>>().Object
        );
    }
}

