using Microsoft.EntityFrameworkCore;
using SkillsBarter.Constants;
using SkillsBarter.Data;
using SkillsBarter.DTOs;
using SkillsBarter.Models;

namespace SkillsBarter.Services;

public class MilestoneService : IMilestoneService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly INotificationService _notificationService;
    private readonly ILogger<MilestoneService> _logger;

    public MilestoneService(
        ApplicationDbContext dbContext,
        INotificationService notificationService,
        ILogger<MilestoneService> logger)
    {
        _dbContext = dbContext;
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task<MilestoneResponse?> CreateMilestoneAsync(Guid agreementId, CreateMilestoneRequest request)
    {
        try
        {
            var agreement = await _dbContext.Agreements
                .Include(a => a.Requester)
                .Include(a => a.Provider)
                .FirstOrDefaultAsync(a => a.Id == agreementId);

            if (agreement == null)
            {
                _logger.LogWarning("Create milestone failed: Agreement {AgreementId} not found", agreementId);
                return null;
            }

            if (string.IsNullOrWhiteSpace(request.Title))
            {
                _logger.LogWarning("Create milestone failed: Title is required");
                return null;
            }

            if (request.DurationInDays <= 0)
            {
                _logger.LogWarning("Create milestone failed: Duration must be positive");
                return null;
            }

            var milestone = new Milestone
            {
                Id = Guid.NewGuid(),
                AgreementId = agreementId,
                Title = request.Title.Trim(),
                DurationInDays = request.DurationInDays,
                Status = MilestoneStatus.Pending,
                DueAt = request.DueAt
            };

            _dbContext.Milestones.Add(milestone);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Milestone {MilestoneId} created for agreement {AgreementId}", milestone.Id, agreementId);

            return MapToMilestoneResponse(milestone);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating milestone for agreement {AgreementId}", agreementId);
            throw;
        }
    }

    public async Task<MilestoneResponse?> GetMilestoneByIdAsync(Guid milestoneId)
    {
        try
        {
            var milestone = await _dbContext.Milestones
                .FirstOrDefaultAsync(m => m.Id == milestoneId);

            if (milestone == null)
            {
                _logger.LogWarning("Milestone {MilestoneId} not found", milestoneId);
                return null;
            }

            return MapToMilestoneResponse(milestone);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving milestone {MilestoneId}", milestoneId);
            throw;
        }
    }

    public async Task<List<MilestoneResponse>> GetMilestonesByAgreementIdAsync(Guid agreementId)
    {
        try
        {
            var milestones = await _dbContext.Milestones
                .Where(m => m.AgreementId == agreementId)
                .OrderBy(m => m.DueAt)
                .ToListAsync();

            return milestones.Select(MapToMilestoneResponse).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving milestones for agreement {AgreementId}", agreementId);
            throw;
        }
    }

    public async Task<MilestoneResponse?> UpdateMilestoneAsync(Guid milestoneId, UpdateMilestoneRequest request)
    {
        try
        {
            var milestone = await _dbContext.Milestones.FindAsync(milestoneId);

            if (milestone == null)
            {
                _logger.LogWarning("Update milestone failed: Milestone {MilestoneId} not found", milestoneId);
                return null;
            }

            if (milestone.Status == MilestoneStatus.Completed)
            {
                _logger.LogWarning("Update milestone failed: Cannot update completed milestone {MilestoneId}", milestoneId);
                return null;
            }

            if (!string.IsNullOrWhiteSpace(request.Title))
            {
                milestone.Title = request.Title.Trim();
            }

            if (request.DurationInDays.HasValue)
            {
                if (request.DurationInDays.Value <= 0)
                {
                    _logger.LogWarning("Update milestone failed: Duration must be positive");
                    return null;
                }
                milestone.DurationInDays = request.DurationInDays.Value;
            }

            if (request.DueAt.HasValue)
            {
                milestone.DueAt = request.DueAt.Value;
            }

            _dbContext.Milestones.Update(milestone);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Milestone {MilestoneId} updated successfully", milestoneId);

            return MapToMilestoneResponse(milestone);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating milestone {MilestoneId}", milestoneId);
            throw;
        }
    }

    public async Task<bool> DeleteMilestoneAsync(Guid milestoneId)
    {
        try
        {
            var milestone = await _dbContext.Milestones.FindAsync(milestoneId);

            if (milestone == null)
            {
                _logger.LogWarning("Delete milestone failed: Milestone {MilestoneId} not found", milestoneId);
                return false;
            }

            if (milestone.Status == MilestoneStatus.Completed)
            {
                _logger.LogWarning("Delete milestone failed: Cannot delete completed milestone {MilestoneId}", milestoneId);
                return false;
            }

            _dbContext.Milestones.Remove(milestone);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Milestone {MilestoneId} deleted successfully", milestoneId);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting milestone {MilestoneId}", milestoneId);
            throw;
        }
    }

    public async Task<MilestoneResponse?> MarkMilestoneAsCompletedAsync(Guid milestoneId)
    {
        try
        {
            var milestone = await _dbContext.Milestones
                .Include(m => m.Agreement)
                    .ThenInclude(a => a.Requester)
                .Include(m => m.Agreement)
                    .ThenInclude(a => a.Provider)
                .FirstOrDefaultAsync(m => m.Id == milestoneId);

            if (milestone == null)
            {
                _logger.LogWarning("Mark milestone completed failed: Milestone {MilestoneId} not found", milestoneId);
                return null;
            }

            if (milestone.Status == MilestoneStatus.Completed)
            {
                _logger.LogWarning("Mark milestone completed failed: Milestone {MilestoneId} is already completed", milestoneId);
                return null;
            }

            milestone.Status = MilestoneStatus.Completed;
            _dbContext.Milestones.Update(milestone);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Milestone {MilestoneId} marked as completed", milestoneId);

            await _notificationService.CreateAsync(
                milestone.Agreement.RequesterId,
                NotificationType.MilestoneCompleted,
                "Milestone Completed",
                $"Milestone '{milestone.Title}' has been completed"
            );
            await _notificationService.CreateAsync(
                milestone.Agreement.ProviderId,
                NotificationType.MilestoneCompleted,
                "Milestone Completed",
                $"Milestone '{milestone.Title}' has been completed"
            );

            return MapToMilestoneResponse(milestone);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking milestone {MilestoneId} as completed", milestoneId);
            throw;
        }
    }

    private MilestoneResponse MapToMilestoneResponse(Milestone milestone)
    {
        return new MilestoneResponse
        {
            Id = milestone.Id,
            AgreementId = milestone.AgreementId,
            Title = milestone.Title,
            DurationInDays = milestone.DurationInDays,
            Status = milestone.Status,
            DueAt = milestone.DueAt
        };
    }
}
