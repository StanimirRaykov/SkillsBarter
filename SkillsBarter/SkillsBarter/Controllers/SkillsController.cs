using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SkillsBarter.DTOs;
using SkillsBarter.Services;

namespace SkillsBarter.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SkillsController : ControllerBase
{
    private readonly ISkillService _skillService;
    private readonly ILogger<SkillsController> _logger;

    public SkillsController(
        ISkillService skillService,
        ILogger<SkillsController> logger)
    {
        _skillService = skillService;
        _logger = logger;
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> CreateSkill([FromBody] CreateSkillRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Invalid model state for create skill request");
                return BadRequest(new { message = "Invalid request", errors = ModelState.Values.SelectMany(v => v.Errors) });
            }

            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return BadRequest(new { message = "Skill name is required and cannot be empty" });
            }

            if (string.IsNullOrWhiteSpace(request.CategoryCode))
            {
                return BadRequest(new { message = "Category code is required and cannot be empty" });
            }

            var skillResponse = await _skillService.CreateSkillAsync(request);
            if (skillResponse == null)
            {
                return BadRequest(new { message = "Failed to create skill. The category may not exist or a skill with this name already exists in the category." });
            }

            _logger.LogInformation("Skill created successfully: {SkillId}", skillResponse.Id);
            return CreatedAtAction(nameof(GetSkillById), new { id = skillResponse.Id }, skillResponse);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating skill");
            return StatusCode(500, new { message = "An error occurred while creating the skill" });
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetSkills([FromQuery] GetSkillsRequest request)
    {
        try
        {
            var skills = await _skillService.GetSkillsAsync(request);
            return Ok(skills);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving skills");
            return StatusCode(500, new { message = "An error occurred while retrieving skills" });
        }
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetSkillById(int id)
    {
        try
        {
            var skill = await _skillService.GetSkillByIdAsync(id);
            if (skill == null)
            {
                return NotFound(new { message = "Skill not found" });
            }

            return Ok(skill);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving skill by ID: {SkillId}", id);
            return StatusCode(500, new { message = "An error occurred while retrieving the skill" });
        }
    }
}
