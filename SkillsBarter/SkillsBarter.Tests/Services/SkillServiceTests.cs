using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using SkillsBarter.Data;
using SkillsBarter.DTOs;
using SkillsBarter.Models;
using SkillsBarter.Services;
using Xunit;

namespace SkillsBarter.Tests.Services;

public class SkillServiceTests
{
    private readonly Mock<ILogger<SkillService>> _mockLogger;
    private ApplicationDbContext _context = null!;
    private SkillService _skillService = null!;

    public SkillServiceTests()
    {
        _mockLogger = new Mock<ILogger<SkillService>>();
        SetupInMemoryDatabase();
    }

    private void SetupInMemoryDatabase()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        _skillService = new SkillService(_context, _mockLogger.Object);
    }

    [Fact]
    public async Task CreateSkillAsync_WithValidData_ReturnsSkillResponse()
    {
        var category = new SkillCategory { Code = "TECH", Label = "Technology" };
        _context.SkillCategories.Add(category);
        await _context.SaveChangesAsync();

        var request = new CreateSkillRequest
        {
            Name = "C# Programming",
            CategoryCode = "TECH"
        };

        var result = await _skillService.CreateSkillAsync(request);

        Assert.NotNull(result);
        Assert.Equal("C# Programming", result.Name);
        Assert.Equal("TECH", result.CategoryCode);
        Assert.Equal("Technology", result.CategoryLabel);
        Assert.True(result.Id > 0);
    }

    [Fact]
    public async Task CreateSkillAsync_WithEmptyName_ReturnsNull()
    {
        var request = new CreateSkillRequest
        {
            Name = "",
            CategoryCode = "TECH"
        };

        var result = await _skillService.CreateSkillAsync(request);

        Assert.Null(result);
    }

    [Fact]
    public async Task CreateSkillAsync_WithWhitespaceName_ReturnsNull()
    {
        var request = new CreateSkillRequest
        {
            Name = "   ",
            CategoryCode = "TECH"
        };

        var result = await _skillService.CreateSkillAsync(request);

        Assert.Null(result);
    }

    [Fact]
    public async Task CreateSkillAsync_WithEmptyCategoryCode_ReturnsNull()
    {
        var request = new CreateSkillRequest
        {
            Name = "C# Programming",
            CategoryCode = ""
        };

        var result = await _skillService.CreateSkillAsync(request);

        Assert.Null(result);
    }

    [Fact]
    public async Task CreateSkillAsync_WithNonExistentCategory_ReturnsNull()
    {
        var request = new CreateSkillRequest
        {
            Name = "C# Programming",
            CategoryCode = "NONEXISTENT"
        };

        var result = await _skillService.CreateSkillAsync(request);

        Assert.Null(result);
    }

    [Fact]
    public async Task CreateSkillAsync_WithDuplicateSkillName_ReturnsNull()
    {
        var category = new SkillCategory { Code = "TECH", Label = "Technology" };
        _context.SkillCategories.Add(category);

        var existingSkill = new Skill
        {
            Name = "C# Programming",
            CategoryCode = "TECH"
        };
        _context.Skills.Add(existingSkill);
        await _context.SaveChangesAsync();

        var request = new CreateSkillRequest
        {
            Name = "C# Programming",
            CategoryCode = "TECH"
        };

        var result = await _skillService.CreateSkillAsync(request);

        Assert.Null(result);
    }

    [Fact]
    public async Task CreateSkillAsync_TrimsSkillName()
    {
        var category = new SkillCategory { Code = "TECH", Label = "Technology" };
        _context.SkillCategories.Add(category);
        await _context.SaveChangesAsync();

        var request = new CreateSkillRequest
        {
            Name = "  C# Programming  ",
            CategoryCode = "TECH"
        };

        var result = await _skillService.CreateSkillAsync(request);

        Assert.NotNull(result);
        Assert.Equal("C# Programming", result.Name);
    }

    [Fact]
    public async Task GetSkillsAsync_WithNoFilters_ReturnsAllSkills()
    {
        var category = new SkillCategory { Code = "TECH", Label = "Technology" };
        _context.SkillCategories.Add(category);

        var skills = new List<Skill>
        {
            new Skill { Name = "C#", CategoryCode = "TECH" },
            new Skill { Name = "Java", CategoryCode = "TECH" },
            new Skill { Name = "Python", CategoryCode = "TECH" }
        };
        _context.Skills.AddRange(skills);
        await _context.SaveChangesAsync();

        var request = new GetSkillsRequest
        {
            Page = 1,
            PageSize = 10
        };

        var result = await _skillService.GetSkillsAsync(request);

        Assert.NotNull(result);
        Assert.Equal(3, result.Total);
        Assert.Equal(3, result.Items.Count);
    }

    [Fact]
    public async Task GetSkillsAsync_WithCategoryFilter_ReturnsFilteredSkills()
    {
        var techCategory = new SkillCategory { Code = "TECH", Label = "Technology" };
        var artCategory = new SkillCategory { Code = "ART", Label = "Art" };
        _context.SkillCategories.AddRange(techCategory, artCategory);

        var skills = new List<Skill>
        {
            new Skill { Name = "C#", CategoryCode = "TECH" },
            new Skill { Name = "Java", CategoryCode = "TECH" },
            new Skill { Name = "Drawing", CategoryCode = "ART" }
        };
        _context.Skills.AddRange(skills);
        await _context.SaveChangesAsync();

        var request = new GetSkillsRequest
        {
            Page = 1,
            PageSize = 10,
            CategoryCode = "TECH"
        };

        var result = await _skillService.GetSkillsAsync(request);

        Assert.NotNull(result);
        Assert.Equal(2, result.Total);
        Assert.Equal(2, result.Items.Count);
        Assert.All(result.Items, item => Assert.Equal("TECH", item.CategoryCode));
    }

    [Fact]
    public async Task GetSkillsAsync_WithSearchQuery_ReturnsMatchingSkills()
    {
        var category = new SkillCategory { Code = "TECH", Label = "Technology" };
        _context.SkillCategories.Add(category);

        var skills = new List<Skill>
        {
            new Skill { Name = "C# Programming", CategoryCode = "TECH" },
            new Skill { Name = "Java Programming", CategoryCode = "TECH" },
            new Skill { Name = "Python", CategoryCode = "TECH" }
        };
        _context.Skills.AddRange(skills);
        await _context.SaveChangesAsync();

        var request = new GetSkillsRequest
        {
            Page = 1,
            PageSize = 10,
            Q = "Programming"
        };

        var result = await _skillService.GetSkillsAsync(request);

        Assert.NotNull(result);
        Assert.Equal(2, result.Total);
        Assert.Equal(2, result.Items.Count);
        Assert.All(result.Items, item => Assert.Contains("Programming", item.Name));
    }

    [Fact]
    public async Task GetSkillsAsync_WithPagination_ReturnsCorrectPage()
    {
        var category = new SkillCategory { Code = "TECH", Label = "Technology" };
        _context.SkillCategories.Add(category);

        var skills = new List<Skill>
        {
            new Skill { Name = "Skill1", CategoryCode = "TECH" },
            new Skill { Name = "Skill2", CategoryCode = "TECH" },
            new Skill { Name = "Skill3", CategoryCode = "TECH" },
            new Skill { Name = "Skill4", CategoryCode = "TECH" },
            new Skill { Name = "Skill5", CategoryCode = "TECH" }
        };
        _context.Skills.AddRange(skills);
        await _context.SaveChangesAsync();

        var request = new GetSkillsRequest
        {
            Page = 2,
            PageSize = 2
        };

        var result = await _skillService.GetSkillsAsync(request);

        Assert.NotNull(result);
        Assert.Equal(5, result.Total);
        Assert.Equal(2, result.Items.Count);
        Assert.Equal(2, result.Page);
        Assert.Equal(2, result.PageSize);
    }

    [Fact]
    public async Task GetSkillsAsync_WithInvalidPaging_AdjustsToDefaults()
    {
        var category = new SkillCategory { Code = "TECH", Label = "Technology" };
        _context.SkillCategories.Add(category);

        var skill = new Skill { Name = "Skill1", CategoryCode = "TECH" };
        _context.Skills.Add(skill);
        await _context.SaveChangesAsync();

        var request = new GetSkillsRequest
        {
            Page = 0,
            PageSize = 0
        };

        var result = await _skillService.GetSkillsAsync(request);

        Assert.Equal(1, result.Page);
        Assert.Equal(10, result.PageSize);
        Assert.Single(result.Items);
    }

    [Fact]
    public async Task GetSkillsAsync_WithLargePageSize_LimitsToHundred()
    {
        var category = new SkillCategory { Code = "TECH", Label = "Technology" };
        _context.SkillCategories.Add(category);

        var skills = Enumerable.Range(1, 150).Select(i => new Skill
        {
            Name = $"Skill{i:D3}",
            CategoryCode = "TECH"
        });

        _context.Skills.AddRange(skills);
        await _context.SaveChangesAsync();

        var request = new GetSkillsRequest
        {
            Page = 1,
            PageSize = 500
        };

        var result = await _skillService.GetSkillsAsync(request);

        Assert.Equal(150, result.Total);
        Assert.Equal(100, result.PageSize);
        Assert.Equal(100, result.Items.Count);
        Assert.True(result.HasNextPage);
    }

    [Fact]
    public async Task GetSkillByIdAsync_WithValidId_ReturnsSkill()
    {
        var category = new SkillCategory { Code = "TECH", Label = "Technology" };
        _context.SkillCategories.Add(category);

        var skill = new Skill { Name = "C#", CategoryCode = "TECH" };
        _context.Skills.Add(skill);
        await _context.SaveChangesAsync();

        var result = await _skillService.GetSkillByIdAsync(skill.Id);

        Assert.NotNull(result);
        Assert.Equal(skill.Id, result.Id);
        Assert.Equal("C#", result.Name);
        Assert.Equal("TECH", result.CategoryCode);
        Assert.Equal("Technology", result.CategoryLabel);
    }

    [Fact]
    public async Task GetSkillByIdAsync_WithInvalidId_ReturnsNull()
    {
        var result = await _skillService.GetSkillByIdAsync(999);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetSkillsAsync_OrdersResultsByName()
    {
        var category = new SkillCategory { Code = "TECH", Label = "Technology" };
        _context.SkillCategories.Add(category);

        var skills = new List<Skill>
        {
            new Skill { Name = "Zebra", CategoryCode = "TECH" },
            new Skill { Name = "Apple", CategoryCode = "TECH" },
            new Skill { Name = "Mango", CategoryCode = "TECH" }
        };
        _context.Skills.AddRange(skills);
        await _context.SaveChangesAsync();

        var request = new GetSkillsRequest
        {
            Page = 1,
            PageSize = 10
        };

        var result = await _skillService.GetSkillsAsync(request);

        Assert.NotNull(result);
        Assert.Equal(3, result.Items.Count);
        Assert.Equal("Apple", result.Items[0].Name);
        Assert.Equal("Mango", result.Items[1].Name);
        Assert.Equal("Zebra", result.Items[2].Name);
    }
}
