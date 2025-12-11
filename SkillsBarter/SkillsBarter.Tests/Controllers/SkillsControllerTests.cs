using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using SkillsBarter.Controllers;
using SkillsBarter.DTOs;
using SkillsBarter.Services;
using Xunit;

namespace SkillsBarter.Tests.Controllers;

public class SkillsControllerTests
{
    private readonly Mock<ISkillService> _skillServiceMock = new();
    private readonly Mock<ILogger<SkillsController>> _loggerMock = new();
    private readonly SkillsController _controller;

    public SkillsControllerTests()
    {
        _controller = new SkillsController(_skillServiceMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task CreateSkill_InvalidModelState_ReturnsBadRequest()
    {
        _controller.ModelState.AddModelError("Name", "Required");
        var request = new CreateSkillRequest { Name = "Skill", CategoryCode = "TECH" };

        var result = await _controller.CreateSkill(request);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Invalid request", GetMessage(badRequest.Value));
        _skillServiceMock.Verify(s => s.CreateSkillAsync(It.IsAny<CreateSkillRequest>()), Times.Never);
    }

    [Fact]
    public async Task CreateSkill_EmptyName_ReturnsBadRequest()
    {
        var request = new CreateSkillRequest { Name = " ", CategoryCode = "TECH" };

        var result = await _controller.CreateSkill(request);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Skill name is required and cannot be empty", GetMessage(badRequest.Value));
        _skillServiceMock.Verify(s => s.CreateSkillAsync(It.IsAny<CreateSkillRequest>()), Times.Never);
    }

    [Fact]
    public async Task CreateSkill_EmptyCategory_ReturnsBadRequest()
    {
        var request = new CreateSkillRequest { Name = "Skill", CategoryCode = " " };

        var result = await _controller.CreateSkill(request);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Category code is required and cannot be empty", GetMessage(badRequest.Value));
        _skillServiceMock.Verify(s => s.CreateSkillAsync(It.IsAny<CreateSkillRequest>()), Times.Never);
    }

    [Fact]
    public async Task CreateSkill_ServiceReturnsNull_ReturnsBadRequest()
    {
        var request = new CreateSkillRequest { Name = "Skill", CategoryCode = "TECH" };
        _skillServiceMock.Setup(s => s.CreateSkillAsync(request)).ReturnsAsync((SkillResponse?)null);

        var result = await _controller.CreateSkill(request);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(
            "Failed to create skill. The category may not exist or a skill with this name already exists in the category.",
            GetMessage(badRequest.Value));
    }

    [Fact]
    public async Task CreateSkill_Success_ReturnsCreatedAtAction()
    {
        var request = new CreateSkillRequest { Name = "Skill", CategoryCode = "TECH" };
        var response = new SkillResponse { Id = 1, Name = "Skill", CategoryCode = "TECH", CategoryLabel = "Technology" };
        _skillServiceMock.Setup(s => s.CreateSkillAsync(request)).ReturnsAsync(response);
        var result = await _controller.CreateSkill(request);
        var createdAt = Assert.IsType<CreatedAtActionResult>(result);
        Assert.Equal(nameof(SkillsController.GetSkillById), createdAt.ActionName);
        Assert.Equal(response.Id, createdAt.RouteValues?["id"]);
        Assert.Equal(response, createdAt.Value);
    }

    [Fact]
    public async Task GetSkills_ReturnsOkWithData()
    {
        var request = new GetSkillsRequest { Page = 1, PageSize = 5 };
        var expectedResponse = new PaginatedResponse<SkillResponse>
        {
            Items = [new SkillResponse { Id = 1, Name = "Skill", CategoryCode = "TECH", CategoryLabel = "Technology" }],
            Page = 1,
            PageSize = 5,
            Total = 1
        };

        _skillServiceMock.Setup(s => s.GetSkillsAsync(request)).ReturnsAsync(expectedResponse);

        var result = await _controller.GetSkills(request);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<PaginatedResponse<SkillResponse>>(okResult.Value);
        Assert.Equal(expectedResponse, payload);
    }

    [Fact]
    public async Task GetSkillById_NotFound_ReturnsNotFound()
    {
        _skillServiceMock.Setup(s => s.GetSkillByIdAsync(5)).ReturnsAsync((SkillResponse?)null);

        var result = await _controller.GetSkillById(5);

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal("Skill not found", GetMessage(notFound.Value));
    }

    [Fact]
    public async Task GetSkillById_Success_ReturnsOk()
    {
        var response = new SkillResponse { Id = 2, Name = "Skill", CategoryCode = "TECH", CategoryLabel = "Technology" };
        _skillServiceMock.Setup(s => s.GetSkillByIdAsync(2)).ReturnsAsync(response);

        var result = await _controller.GetSkillById(2);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(response, okResult.Value);
    }

    [Fact]
    public async Task CreateSkill_OnException_ReturnsServerError()
    {
        var request = new CreateSkillRequest { Name = "Skill", CategoryCode = "TECH" };
        _skillServiceMock.Setup(s => s.CreateSkillAsync(request)).ThrowsAsync(new Exception("boom"));

        var result = await _controller.CreateSkill(request);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, objectResult.StatusCode);
        Assert.Equal("An error occurred while creating the skill", GetMessage(objectResult.Value));
    }

    [Fact]
    public async Task GetSkills_OnException_ReturnsServerError()
    {
        var request = new GetSkillsRequest();
        _skillServiceMock.Setup(s => s.GetSkillsAsync(request)).ThrowsAsync(new Exception("boom"));

        var result = await _controller.GetSkills(request);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, objectResult.StatusCode);
        Assert.Equal("An error occurred while retrieving skills", GetMessage(objectResult.Value));
    }

    [Fact]
    public async Task GetSkillById_OnException_ReturnsServerError()
    {
        _skillServiceMock.Setup(s => s.GetSkillByIdAsync(3)).ThrowsAsync(new Exception("boom"));

        var result = await _controller.GetSkillById(3);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, objectResult.StatusCode);
        Assert.Equal("An error occurred while retrieving the skill", GetMessage(objectResult.Value));
    }

    private static string? GetMessage(object? value) =>
        value?.GetType().GetProperty("message")?.GetValue(value)?.ToString();
}