using Microsoft.EntityFrameworkCore;
using SkillsBarter.Data;
using SkillsBarter.DTOs;
using SkillsBarter.Models;

namespace SkillsBarter.Services;

public class SkillService : ISkillService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<SkillService> _logger;

    public SkillService(ApplicationDbContext dbContext, ILogger<SkillService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<SkillResponse?> CreateSkillAsync(CreateSkillRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                _logger.LogWarning("Create skill failed: Empty name provided");
                return null;
            }

            if (string.IsNullOrWhiteSpace(request.CategoryCode))
            {
                _logger.LogWarning("Create skill failed: Empty category code provided");
                return null;
            }

            var category = await _dbContext.SkillCategories.FirstOrDefaultAsync(c => c.Code == request.CategoryCode);
            if (category == null)
            {
                _logger.LogWarning("Create skill failed: Category {CategoryCode} not found", request.CategoryCode);
                return null;
            }

            var existingSkill = await _dbContext.Skills.FirstOrDefaultAsync(s => s.Name == request.Name.Trim() && s.CategoryCode == request.CategoryCode);
            if (existingSkill != null)
            {
                _logger.LogWarning("Create skill failed: Skill with name '{Name}' already exists in category {CategoryCode}", request.Name, request.CategoryCode);
                return null;
            }

            var skill = new Skill
            {
                Name = request.Name.Trim(),
                CategoryCode = request.CategoryCode
            };

            _dbContext.Skills.Add(skill);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Skill created successfully: {SkillId} - {SkillName}", skill.Id, skill.Name);

            return MapToSkillResponse(skill, category.Label);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating skill");
            throw;
        }
    }

    public async Task<PaginatedResponse<SkillResponse>> GetSkillsAsync(GetSkillsRequest request)
    {
        try
        {
            request.Validate();

            var query = _dbContext.Skills
                .Include(s => s.Category)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(request.CategoryCode))
            {
                query = query.Where(s => s.CategoryCode == request.CategoryCode);
            }

            if (!string.IsNullOrWhiteSpace(request.Q))
            {
                var keyword = request.Q.ToLower();
                query = query.Where(s => s.Name.ToLower().Contains(keyword));
            }

            var total = await query.CountAsync();

            var skills = await query
                .OrderBy(s => s.Name)
                .Skip((request.Page - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToListAsync();

            var skillResponses = skills.Select(s => MapToSkillResponse(s, s.Category?.Label ?? string.Empty)).ToList();

            return new PaginatedResponse<SkillResponse>
            {
                Items = skillResponses,
                Page = request.Page,
                PageSize = request.PageSize,
                Total = total
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving skills with filters: {@Request}", request);
            throw;
        }
    }

    public async Task<SkillResponse?> GetSkillByIdAsync(int id)
    {
        try
        {
            var skill = await _dbContext.Skills
                .Include(s => s.Category)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (skill == null)
            {
                _logger.LogWarning("Skill not found: {SkillId}", id);
                return null;
            }

            return MapToSkillResponse(skill, skill.Category?.Label ?? string.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving skill by ID: {SkillId}", id);
            throw;
        }
    }

    private SkillResponse MapToSkillResponse(Skill skill, string categoryLabel)
    {
        return new SkillResponse
        {
            Id = skill.Id,
            Name = skill.Name,
            CategoryCode = skill.CategoryCode,
            CategoryLabel = categoryLabel
        };
    }
}
