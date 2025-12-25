using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using SkillsBarter.Data;
using SkillsBarter.DTOs;
using SkillsBarter.Models;
using SkillsBarter.Services;
using Xunit;

namespace SkillsBarter.Tests.Services;

public class OfferServiceTests
{
    private readonly Mock<ILogger<OfferService>> _mockLogger;
    private readonly Mock<INotificationService> _mockNotificationService;
    private readonly Mock<UserManager<ApplicationUser>> _mockUserManager;
    private ApplicationDbContext _context = null!;
    private OfferService _offerService = null!;

    public OfferServiceTests()
    {
        _mockLogger = new Mock<ILogger<OfferService>>();
        _mockNotificationService = new Mock<INotificationService>();
        _mockUserManager = new Mock<UserManager<ApplicationUser>>(
            Mock.Of<IUserStore<ApplicationUser>>(),
            null!, null!, null!, null!, null!, null!, null!, null!);
        SetupInMemoryDatabase();
    }

    private void SetupInMemoryDatabase()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        _offerService = new OfferService(_context, _mockNotificationService.Object, _mockLogger.Object, _mockUserManager.Object);
    }

    private async Task<(ApplicationUser user, Skill skill)> SetupTestDataAsync()
    {
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Name = "Test User",
            UserName = "testuser",
            Email = "test@example.com"
        };
        _context.Users.Add(user);

        var category = new SkillCategory { Code = "TECH", Label = "Technology" };
        _context.SkillCategories.Add(category);

        var skill = new Skill { Name = "C# Programming", CategoryCode = "TECH" };
        _context.Skills.Add(skill);

        var offerStatus = new OfferStatus { Code = OfferStatusCode.Active, Label = "Active" };
        _context.OfferStatuses.Add(offerStatus);

        var cancelledStatus = new OfferStatus { Code = OfferStatusCode.Cancelled, Label = "Cancelled" };
        _context.OfferStatuses.Add(cancelledStatus);

        await _context.SaveChangesAsync();

        _mockUserManager.Setup(um => um.FindByIdAsync(user.Id.ToString()))
            .ReturnsAsync(user);
        _mockUserManager.Setup(um => um.GetRolesAsync(user))
            .ReturnsAsync(new List<string> { "Premium" });

        return (user, skill);
    }


    [Fact]
    public async Task CreateOfferAsync_WithValidData_ReturnsOfferResponse()
    {
        var (user, skill) = await SetupTestDataAsync();
        var request = new CreateOfferRequest
        {
            Title = "Test Offer",
            Description = "Test Description",
            SkillId = skill.Id
        };

        var result = await _offerService.CreateOfferAsync(user.Id, request);

        Assert.NotNull(result);
        Assert.Equal("Test Offer", result.Title);
        Assert.Equal("Test Description", result.Description);
        Assert.Equal(skill.Id, result.SkillId);
        Assert.Equal(user.Id, result.UserId);
        Assert.Equal("Active", result.StatusCode);
    }

    [Fact]
    public async Task CreateOfferAsync_WithEmptyTitle_ReturnsNull()
    {
        var (user, skill) = await SetupTestDataAsync();
        var request = new CreateOfferRequest
        {
            Title = "",
            Description = "Test Description",
            SkillId = skill.Id
        };

        var result = await _offerService.CreateOfferAsync(user.Id, request);

        Assert.Null(result);
    }

    [Fact]
    public async Task CreateOfferAsync_WithWhitespaceTitle_ReturnsNull()
    {
        var (user, skill) = await SetupTestDataAsync();
        var request = new CreateOfferRequest
        {
            Title = "   ",
            Description = "Test Description",
            SkillId = skill.Id
        };

        var result = await _offerService.CreateOfferAsync(user.Id, request);

        Assert.Null(result);
    }

    [Fact]
    public async Task CreateOfferAsync_WithEmptyDescription_ReturnsNull()
    {
        var (user, skill) = await SetupTestDataAsync();
        var request = new CreateOfferRequest
        {
            Title = "Test Offer",
            Description = "",
            SkillId = skill.Id
        };

        var result = await _offerService.CreateOfferAsync(user.Id, request);

        Assert.Null(result);
    }

    [Fact]
    public async Task CreateOfferAsync_WithNonExistentSkill_ReturnsNull()
    {
        var (user, _) = await SetupTestDataAsync();
        var request = new CreateOfferRequest
        {
            Title = "Test Offer",
            Description = "Test Description",
            SkillId = 999
        };

        var result = await _offerService.CreateOfferAsync(user.Id, request);

        Assert.Null(result);
    }

    [Fact]
    public async Task CreateOfferAsync_TrimsTitle()
    {
        var (user, skill) = await SetupTestDataAsync();
        var request = new CreateOfferRequest
        {
            Title = "  Test Offer  ",
            Description = "Test Description",
            SkillId = skill.Id
        };

        var result = await _offerService.CreateOfferAsync(user.Id, request);

        Assert.NotNull(result);
        Assert.Equal("Test Offer", result.Title);
    }

    [Fact]
    public async Task CreateOfferAsync_TrimsDescription()
    {
        var (user, skill) = await SetupTestDataAsync();
        var request = new CreateOfferRequest
        {
            Title = "Test Offer",
            Description = "  Test Description  ",
            SkillId = skill.Id
        };

        var result = await _offerService.CreateOfferAsync(user.Id, request);

        Assert.NotNull(result);
        Assert.Equal("Test Description", result.Description);
    }

    [Fact]
    public async Task CreateOfferAsync_WithNullDescription_ReturnsNull()
    {
        var (user, skill) = await SetupTestDataAsync();
        var request = new CreateOfferRequest
        {
            Title = "Test Offer",
            Description = null,
            SkillId = skill.Id
        };

        var result = await _offerService.CreateOfferAsync(user.Id, request);

        Assert.Null(result);
    }


    [Fact]
    public async Task GetOffersAsync_WithNoFilters_ReturnsActiveOffers()
    {
        var (user, skill) = await SetupTestDataAsync();

        var offers = new List<Offer>
        {
            new Offer
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                SkillId = skill.Id,
                Title = "Active Offer 1",
                Description = "Description 1",
                StatusCode = OfferStatusCode.Active
            },
            new Offer
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                SkillId = skill.Id,
                Title = "Active Offer 2",
                Description = "Description 2",
                StatusCode = OfferStatusCode.Active
            },
            new Offer
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                SkillId = skill.Id,
                Title = "Cancelled Offer",
                Description = "Description 3",
                StatusCode = OfferStatusCode.Cancelled
            }
        };
        _context.Offers.AddRange(offers);
        await _context.SaveChangesAsync();

        var request = new GetOffersRequest { Page = 1, PageSize = 10 };

        var result = await _offerService.GetOffersAsync(request);

        Assert.NotNull(result);
        Assert.Equal(2, result.Total);
        Assert.Equal(2, result.Items.Count);
        Assert.All(result.Items, item => Assert.Equal("Active", item.StatusCode));
    }

    [Fact]
    public async Task GetOffersAsync_WithSkillIdFilter_ReturnsFilteredOffers()
    {
        var (user, skill) = await SetupTestDataAsync();

        var skill2 = new Skill { Name = "Java", CategoryCode = "TECH" };
        _context.Skills.Add(skill2);
        await _context.SaveChangesAsync();

        var offers = new List<Offer>
        {
            new Offer
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                SkillId = skill.Id,
                Title = "C# Offer",
                Description = "Description",
                StatusCode = OfferStatusCode.Active
            },
            new Offer
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                SkillId = skill2.Id,
                Title = "Java Offer",
                Description = "Description",
                StatusCode = OfferStatusCode.Active
            }
        };
        _context.Offers.AddRange(offers);
        await _context.SaveChangesAsync();

        var request = new GetOffersRequest { Page = 1, PageSize = 10, SkillId = skill.Id };

        var result = await _offerService.GetOffersAsync(request);

        Assert.NotNull(result);
        Assert.Equal(1, result.Total);
        Assert.Single(result.Items);
        Assert.Equal("C# Offer", result.Items.First().Title);
    }

    [Fact]
    public async Task GetOffersAsync_WithCategoryFilter_ReturnsFilteredOffers()
    {
        var (user, skill) = await SetupTestDataAsync();

        var artCategory = new SkillCategory { Code = "ART", Label = "Art" };
        _context.SkillCategories.Add(artCategory);

        var artSkill = new Skill { Name = "Drawing", CategoryCode = "ART" };
        _context.Skills.Add(artSkill);
        await _context.SaveChangesAsync();

        var offers = new List<Offer>
        {
            new Offer
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                SkillId = skill.Id,
                Title = "Tech Offer",
                Description = "Description",
                StatusCode = OfferStatusCode.Active
            },
            new Offer
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                SkillId = artSkill.Id,
                Title = "Art Offer",
                Description = "Description",
                StatusCode = OfferStatusCode.Active
            }
        };
        _context.Offers.AddRange(offers);
        await _context.SaveChangesAsync();

        var request = new GetOffersRequest { Page = 1, PageSize = 10, Skill = "TECH" };

        var result = await _offerService.GetOffersAsync(request);

        Assert.NotNull(result);
        Assert.Equal(1, result.Total);
        Assert.Single(result.Items);
        Assert.Equal("Tech Offer", result.Items.First().Title);
    }

    [Fact]
    public async Task GetOffersAsync_WithSearchQuery_ReturnsMatchingOffers()
    {
        var (user, skill) = await SetupTestDataAsync();

        var offers = new List<Offer>
        {
            new Offer
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                SkillId = skill.Id,
                Title = "Learn C# Programming",
                Description = "Description",
                StatusCode = OfferStatusCode.Active
            },
            new Offer
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                SkillId = skill.Id,
                Title = "Python Basics",
                Description = "Description",
                StatusCode = OfferStatusCode.Active
            },
            new Offer
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                SkillId = skill.Id,
                Title = "Something Else",
                Description = "Learn programming here",
                StatusCode = OfferStatusCode.Active
            }
        };
        _context.Offers.AddRange(offers);
        await _context.SaveChangesAsync();

        var request = new GetOffersRequest { Page = 1, PageSize = 10, Q = "programming" };

        var result = await _offerService.GetOffersAsync(request);

        Assert.NotNull(result);
        Assert.Equal(2, result.Total);
    }

    [Fact]
    public async Task GetOffersAsync_WithPagination_ReturnsCorrectPage()
    {
        var (user, skill) = await SetupTestDataAsync();

        var offers = Enumerable.Range(1, 15).Select(i => new Offer
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            SkillId = skill.Id,
            Title = $"Offer {i:D2}",
            Description = "Description",
            StatusCode = OfferStatusCode.Active,
            CreatedAt = DateTime.UtcNow.AddMinutes(-i)
        }).ToList();
        _context.Offers.AddRange(offers);
        await _context.SaveChangesAsync();

        var request = new GetOffersRequest { Page = 2, PageSize = 5 };

        var result = await _offerService.GetOffersAsync(request);

        Assert.NotNull(result);
        Assert.Equal(15, result.Total);
        Assert.Equal(5, result.Items.Count);
        Assert.Equal(2, result.Page);
        Assert.Equal(5, result.PageSize);
    }

    [Fact]
    public async Task GetOffersAsync_WithInvalidPaging_AdjustsToDefaults()
    {
        var (user, skill) = await SetupTestDataAsync();

        var offer = new Offer
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            SkillId = skill.Id,
            Title = "Test Offer",
            Description = "Description",
            StatusCode = OfferStatusCode.Active
        };
        _context.Offers.Add(offer);
        await _context.SaveChangesAsync();

        var request = new GetOffersRequest { Page = 0, PageSize = 0 };

        var result = await _offerService.GetOffersAsync(request);

        Assert.Equal(1, result.Page);
        Assert.Equal(10, result.PageSize);
    }

    [Fact]
    public async Task GetOffersAsync_OrdersByCreatedAtDescending()
    {
        var (user, skill) = await SetupTestDataAsync();

        var baseTime = DateTime.UtcNow;
        var offers = new List<Offer>
        {
            new Offer
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                SkillId = skill.Id,
                Title = "Oldest Offer",
                Description = "Description",
                StatusCode = OfferStatusCode.Active,
                CreatedAt = baseTime.AddDays(-2)
            },
            new Offer
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                SkillId = skill.Id,
                Title = "Newest Offer",
                Description = "Description",
                StatusCode = OfferStatusCode.Active,
                CreatedAt = baseTime
            },
            new Offer
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                SkillId = skill.Id,
                Title = "Middle Offer",
                Description = "Description",
                StatusCode = OfferStatusCode.Active,
                CreatedAt = baseTime.AddDays(-1)
            }
        };
        _context.Offers.AddRange(offers);
        await _context.SaveChangesAsync();

        var request = new GetOffersRequest { Page = 1, PageSize = 10 };

        var result = await _offerService.GetOffersAsync(request);

        Assert.Equal("Newest Offer", result.Items[0].Title);
        Assert.Equal("Middle Offer", result.Items[1].Title);
        Assert.Equal("Oldest Offer", result.Items[2].Title);
    }



    [Fact]
    public async Task GetOfferByIdAsync_WithValidId_ReturnsOfferDetail()
    {
        var (user, skill) = await SetupTestDataAsync();

        var offer = new Offer
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            SkillId = skill.Id,
            Title = "Test Offer",
            Description = "Test Description",
            StatusCode = OfferStatusCode.Active
        };
        _context.Offers.Add(offer);
        await _context.SaveChangesAsync();

        var result = await _offerService.GetOfferByIdAsync(offer.Id);

        Assert.NotNull(result);
        Assert.Equal(offer.Id, result.Id);
        Assert.Equal("Test Offer", result.Title);
        Assert.Equal("Test Description", result.Description);
        Assert.Equal(skill.Id, result.SkillId);
        Assert.Equal(skill.Name, result.SkillName);
        Assert.Equal("TECH", result.SkillCategoryCode);
        Assert.Equal(user.Id, result.Owner.Id);
        Assert.Equal(user.Name, result.Owner.Name);
    }

    [Fact]
    public async Task GetOfferByIdAsync_WithNonExistentId_ReturnsNull()
    {
        await SetupTestDataAsync();

        var result = await _offerService.GetOfferByIdAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task GetOfferByIdAsync_WithInactiveOffer_ReturnsNull()
    {
        var (user, skill) = await SetupTestDataAsync();

        var offer = new Offer
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            SkillId = skill.Id,
            Title = "Cancelled Offer",
            Description = "Description",
            StatusCode = OfferStatusCode.Cancelled
        };
        _context.Offers.Add(offer);
        await _context.SaveChangesAsync();

        var result = await _offerService.GetOfferByIdAsync(offer.Id);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetOfferByIdAsync_CalculatesOwnerRating()
    {
        var (user, skill) = await SetupTestDataAsync();

        var offer = new Offer
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            SkillId = skill.Id,
            Title = "Test Offer",
            Description = "Description",
            StatusCode = OfferStatusCode.Active
        };
        _context.Offers.Add(offer);

        var reviewer = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Name = "Reviewer",
            UserName = "reviewer",
            Email = "reviewer@test.com"
        };
        _context.Users.Add(reviewer);

        var agreement = new Agreement
        {
            Id = Guid.NewGuid(),
            OfferId = offer.Id,
            RequesterId = reviewer.Id,
            ProviderId = user.Id,
            Status = AgreementStatus.Completed
        };
        _context.Agreements.Add(agreement);

        var reviews = new List<Review>
        {
            new Review { Id = Guid.NewGuid(), RecipientId = user.Id, ReviewerId = reviewer.Id, AgreementId = agreement.Id, Rating = 5 },
            new Review { Id = Guid.NewGuid(), RecipientId = user.Id, ReviewerId = reviewer.Id, AgreementId = agreement.Id, Rating = 4 },
            new Review { Id = Guid.NewGuid(), RecipientId = user.Id, ReviewerId = reviewer.Id, AgreementId = agreement.Id, Rating = 3 }
        };
        _context.Reviews.AddRange(reviews);
        await _context.SaveChangesAsync();

        var result = await _offerService.GetOfferByIdAsync(offer.Id);

        Assert.NotNull(result);
        Assert.Equal(4m, result.Owner.Rating);
    }

    [Fact]
    public async Task UpdateOfferAsync_WithValidData_ReturnsUpdatedOffer()
    {
        var (user, skill) = await SetupTestDataAsync();

        var offer = new Offer
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            SkillId = skill.Id,
            Title = "Original Title",
            Description = "Original Description",
            StatusCode = OfferStatusCode.Active
        };
        _context.Offers.Add(offer);
        await _context.SaveChangesAsync();

        var request = new UpdateOfferRequest
        {
            Title = "Updated Title",
            Description = "Updated Description"
        };

        var result = await _offerService.UpdateOfferAsync(offer.Id, user.Id, request, false);

        Assert.NotNull(result);
        Assert.Equal("Updated Title", result.Title);
        Assert.Equal("Updated Description", result.Description);
    }

    [Fact]
    public async Task UpdateOfferAsync_WithNonExistentOffer_ReturnsNull()
    {
        var (user, _) = await SetupTestDataAsync();
        var request = new UpdateOfferRequest { Title = "Updated" };

        var result = await _offerService.UpdateOfferAsync(Guid.NewGuid(), user.Id, request, false);

        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateOfferAsync_UnauthorizedUser_ReturnsNull()
    {
        var (user, skill) = await SetupTestDataAsync();

        var offer = new Offer
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            SkillId = skill.Id,
            Title = "Original Title",
            Description = "Description",
            StatusCode = OfferStatusCode.Active
        };
        _context.Offers.Add(offer);
        await _context.SaveChangesAsync();

        var otherUserId = Guid.NewGuid();
        var request = new UpdateOfferRequest { Title = "Updated" };

        var result = await _offerService.UpdateOfferAsync(offer.Id, otherUserId, request, false);

        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateOfferAsync_AsAdmin_CanUpdateAnyOffer()
    {
        var (user, skill) = await SetupTestDataAsync();

        var offer = new Offer
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            SkillId = skill.Id,
            Title = "Original Title",
            Description = "Description",
            StatusCode = OfferStatusCode.Active
        };
        _context.Offers.Add(offer);
        await _context.SaveChangesAsync();

        var adminId = Guid.NewGuid();
        var request = new UpdateOfferRequest { Title = "Admin Updated" };

        var result = await _offerService.UpdateOfferAsync(offer.Id, adminId, request, true);

        Assert.NotNull(result);
        Assert.Equal("Admin Updated", result.Title);
    }

    [Fact]
    public async Task UpdateOfferAsync_WithNewSkillId_UpdatesSkill()
    {
        var (user, skill) = await SetupTestDataAsync();

        var skill2 = new Skill { Name = "Java", CategoryCode = "TECH" };
        _context.Skills.Add(skill2);
        await _context.SaveChangesAsync();

        var offer = new Offer
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            SkillId = skill.Id,
            Title = "Original Title",
            Description = "Description",
            StatusCode = OfferStatusCode.Active
        };
        _context.Offers.Add(offer);
        await _context.SaveChangesAsync();

        var request = new UpdateOfferRequest { SkillId = skill2.Id };

        var result = await _offerService.UpdateOfferAsync(offer.Id, user.Id, request, false);

        Assert.NotNull(result);
        Assert.Equal(skill2.Id, result.SkillId);
    }

    [Fact]
    public async Task UpdateOfferAsync_WithNonExistentSkillId_ReturnsNull()
    {
        var (user, skill) = await SetupTestDataAsync();

        var offer = new Offer
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            SkillId = skill.Id,
            Title = "Original Title",
            Description = "Description",
            StatusCode = OfferStatusCode.Active
        };
        _context.Offers.Add(offer);
        await _context.SaveChangesAsync();

        var request = new UpdateOfferRequest { SkillId = 999 };

        var result = await _offerService.UpdateOfferAsync(offer.Id, user.Id, request, false);

        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateOfferAsync_WithValidStatusCode_UpdatesStatus()
    {
        var (user, skill) = await SetupTestDataAsync();

        var completedStatus = new OfferStatus { Code = OfferStatusCode.Completed, Label = "Completed" };
        _context.OfferStatuses.Add(completedStatus);

        var offer = new Offer
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            SkillId = skill.Id,
            Title = "Original Title",
            Description = "Description",
            StatusCode = OfferStatusCode.Active
        };
        _context.Offers.Add(offer);
        await _context.SaveChangesAsync();

        var request = new UpdateOfferRequest { StatusCode = "Completed" };

        var result = await _offerService.UpdateOfferAsync(offer.Id, user.Id, request, false);

        Assert.NotNull(result);
        Assert.Equal("Completed", result.StatusCode);
    }

    [Fact]
    public async Task UpdateOfferAsync_WithInvalidStatusCode_ReturnsNull()
    {
        var (user, skill) = await SetupTestDataAsync();

        var offer = new Offer
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            SkillId = skill.Id,
            Title = "Original Title",
            Description = "Description",
            StatusCode = OfferStatusCode.Active
        };
        _context.Offers.Add(offer);
        await _context.SaveChangesAsync();

        var request = new UpdateOfferRequest { StatusCode = "InvalidStatus" };

        var result = await _offerService.UpdateOfferAsync(offer.Id, user.Id, request, false);

        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateOfferAsync_TrimsTitle()
    {
        var (user, skill) = await SetupTestDataAsync();

        var offer = new Offer
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            SkillId = skill.Id,
            Title = "Original Title",
            Description = "Description",
            StatusCode = OfferStatusCode.Active
        };
        _context.Offers.Add(offer);
        await _context.SaveChangesAsync();

        var request = new UpdateOfferRequest { Title = "  Updated Title  " };

        var result = await _offerService.UpdateOfferAsync(offer.Id, user.Id, request, false);

        Assert.NotNull(result);
        Assert.Equal("Updated Title", result.Title);
    }

    [Fact]
    public async Task UpdateOfferAsync_WithEmptyDescription_SetsToNull()
    {
        var (user, skill) = await SetupTestDataAsync();

        var offer = new Offer
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            SkillId = skill.Id,
            Title = "Original Title",
            Description = "Original Description",
            StatusCode = OfferStatusCode.Active
        };
        _context.Offers.Add(offer);
        await _context.SaveChangesAsync();

        var request = new UpdateOfferRequest { Description = "   " };

        var result = await _offerService.UpdateOfferAsync(offer.Id, user.Id, request, false);

        Assert.NotNull(result);
        Assert.Null(result.Description);
    }

    [Fact]
    public async Task UpdateOfferAsync_UpdatesTimestamp()
    {
        var (user, skill) = await SetupTestDataAsync();

        var originalTime = DateTime.UtcNow.AddDays(-1);
        var offer = new Offer
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            SkillId = skill.Id,
            Title = "Original Title",
            Description = "Description",
            StatusCode = OfferStatusCode.Active,
            CreatedAt = originalTime,
            UpdatedAt = originalTime
        };
        _context.Offers.Add(offer);
        await _context.SaveChangesAsync();

        var request = new UpdateOfferRequest { Title = "Updated" };

        var result = await _offerService.UpdateOfferAsync(offer.Id, user.Id, request, false);

        Assert.NotNull(result);
        Assert.True(result.UpdatedAt > originalTime);
    }

    [Fact]
    public async Task DeleteOfferAsync_WithValidOffer_ReturnsTrueAndMarksCancelled()
    {
        var (user, skill) = await SetupTestDataAsync();

        var offer = new Offer
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            SkillId = skill.Id,
            Title = "Test Offer",
            Description = "Description",
            StatusCode = OfferStatusCode.Active
        };
        _context.Offers.Add(offer);
        await _context.SaveChangesAsync();

        var result = await _offerService.DeleteOfferAsync(offer.Id, user.Id, false);

        Assert.True(result);

        var deletedOffer = await _context.Offers.FindAsync(offer.Id);
        Assert.NotNull(deletedOffer);
        Assert.Equal(OfferStatusCode.Cancelled, deletedOffer.StatusCode);
    }

    [Fact]
    public async Task DeleteOfferAsync_WithNonExistentOffer_ReturnsFalse()
    {
        var (user, _) = await SetupTestDataAsync();

        var result = await _offerService.DeleteOfferAsync(Guid.NewGuid(), user.Id, false);

        Assert.False(result);
    }

    [Fact]
    public async Task DeleteOfferAsync_UnauthorizedUser_ReturnsFalse()
    {
        var (user, skill) = await SetupTestDataAsync();

        var offer = new Offer
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            SkillId = skill.Id,
            Title = "Test Offer",
            Description = "Description",
            StatusCode = OfferStatusCode.Active
        };
        _context.Offers.Add(offer);
        await _context.SaveChangesAsync();

        var otherUserId = Guid.NewGuid();

        var result = await _offerService.DeleteOfferAsync(offer.Id, otherUserId, false);

        Assert.False(result);

        var unchangedOffer = await _context.Offers.FindAsync(offer.Id);
        Assert.Equal(OfferStatusCode.Active, unchangedOffer!.StatusCode);
    }

    [Fact]
    public async Task DeleteOfferAsync_AsAdmin_CanDeleteAnyOffer()
    {
        var (user, skill) = await SetupTestDataAsync();

        var offer = new Offer
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            SkillId = skill.Id,
            Title = "Test Offer",
            Description = "Description",
            StatusCode = OfferStatusCode.Active
        };
        _context.Offers.Add(offer);
        await _context.SaveChangesAsync();

        var adminId = Guid.NewGuid();

        var result = await _offerService.DeleteOfferAsync(offer.Id, adminId, true);

        Assert.True(result);

        var deletedOffer = await _context.Offers.FindAsync(offer.Id);
        Assert.Equal(OfferStatusCode.Cancelled, deletedOffer!.StatusCode);
    }

    [Fact]
    public async Task DeleteOfferAsync_UpdatesTimestamp()
    {
        var (user, skill) = await SetupTestDataAsync();

        var originalTime = DateTime.UtcNow.AddDays(-1);
        var offer = new Offer
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            SkillId = skill.Id,
            Title = "Test Offer",
            Description = "Description",
            StatusCode = OfferStatusCode.Active,
            UpdatedAt = originalTime
        };
        _context.Offers.Add(offer);
        await _context.SaveChangesAsync();

        await _offerService.DeleteOfferAsync(offer.Id, user.Id, false);

        var deletedOffer = await _context.Offers.FindAsync(offer.Id);
        Assert.True(deletedOffer!.UpdatedAt > originalTime);
    }


}
