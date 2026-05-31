using Microsoft.EntityFrameworkCore;
using ScoutsAttendance.Application.DTOs.Projects;
using ScoutsAttendance.Application.Interfaces;
using ScoutsAttendance.Domain.Entities;

namespace ScoutsAttendance.Application.Services;

// ─── Interface ────────────────────────────────────────────────────────────────

public interface IProjectService
{
    Task<IEnumerable<ProjectDto>>             GetAllAsync();
    Task<ProjectDto?>                          GetByIdAsync(Guid id);
    Task<ProjectDto>                           CreateAsync(CreateProjectDto dto);
    Task<ProjectDto?>                          UpdateAsync(Guid id, UpdateProjectDto dto);
    Task<bool>                                 DeleteAsync(Guid id);

    /// <summary>All members in the project's group/troop with their score (null if not graded).</summary>
    Task<IEnumerable<ProjectMemberScoreDto>>   GetProjectMembersAsync(Guid projectId);

    /// <summary>Save (insert or update) a member's score for a project.</summary>
    Task<ProjectMemberScoreDto?>               SaveScoreAsync(Guid projectId, Guid memberId, SaveScoreDto dto);

    /// <summary>All graded projects for one member.</summary>
    Task<MemberProjectSummaryDto>              GetMemberSummaryAsync(Guid memberId);
}

// ─── Implementation ───────────────────────────────────────────────────────────

public class ProjectService : IProjectService
{
    private readonly IUnitOfWork         _uow;
    private readonly ICurrentUserService _currentUser;

    public ProjectService(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow         = uow;
        _currentUser = currentUser;
    }

    // ── List ──────────────────────────────────────────────────────────────────

    public async Task<IEnumerable<ProjectDto>> GetAllAsync()
    {
        IQueryable<Project> query = _uow.Projects.Query()
            .Include(p => p.Group)
            .Include(p => p.Troop)
            .Include(p => p.Scores);

        if (!_currentUser.IsSystemAdmin && _currentUser.GroupId.HasValue)
            query = query.Where(p => p.GroupId == _currentUser.GroupId.Value);

        var list = await query.OrderByDescending(p => p.CreatedAt).ToListAsync();
        return list.Select(MapProject);
    }

    // ── Get by id ─────────────────────────────────────────────────────────────

    public async Task<ProjectDto?> GetByIdAsync(Guid id)
    {
        var p = await _uow.Projects.Query()
            .Include(p => p.Group)
            .Include(p => p.Troop)
            .Include(p => p.Scores)
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);
        return p is null ? null : MapProject(p);
    }

    // ── Create ────────────────────────────────────────────────────────────────

    public async Task<ProjectDto> CreateAsync(CreateProjectDto dto)
    {
        var groupId = _currentUser.IsSystemAdmin
            ? (_currentUser.GroupId ?? throw new InvalidOperationException("SystemAdmin must have a GroupId context"))
            : (_currentUser.GroupId ?? throw new InvalidOperationException("No group context"));

        var project = new Project
        {
            Name        = dto.Name.Trim(),
            Description = dto.Description?.Trim(),
            MaxScore    = dto.MaxScore,
            GroupId     = groupId,
            TroopId     = dto.TroopId,
            CreatedBy   = _currentUser.Username,
            IsActive    = true
        };

        await _uow.Projects.AddAsync(project);
        await _uow.SaveChangesAsync();
        return await GetByIdAsync(project.Id) ?? throw new Exception("Failed to retrieve created project");
    }

    // ── Update ────────────────────────────────────────────────────────────────

    public async Task<ProjectDto?> UpdateAsync(Guid id, UpdateProjectDto dto)
    {
        var project = await _uow.Projects.GetByIdAsync(id);
        if (project is null) return null;

        project.Name        = dto.Name.Trim();
        project.Description = dto.Description?.Trim();
        project.MaxScore    = dto.MaxScore;
        project.TroopId     = dto.TroopId;
        project.IsActive    = dto.IsActive;
        project.UpdatedAt   = DateTime.UtcNow;

        _uow.Projects.Update(project);
        await _uow.SaveChangesAsync();
        return await GetByIdAsync(id);
    }

    // ── Delete ────────────────────────────────────────────────────────────────

    public async Task<bool> DeleteAsync(Guid id)
    {
        var project = await _uow.Projects.GetByIdAsync(id);
        if (project is null) return false;
        _uow.Projects.SoftDelete(project);
        await _uow.SaveChangesAsync();
        return true;
    }

    // ── Get members + scores ──────────────────────────────────────────────────

    public async Task<IEnumerable<ProjectMemberScoreDto>> GetProjectMembersAsync(Guid projectId)
    {
        var project = await _uow.Projects.GetByIdAsync(projectId);
        if (project is null) return [];

        // Load members scoped to the project's group/troop
        var membersQuery = _uow.Members.Query()
            .Include(m => m.Troop)
            .Where(m => m.GroupId == project.GroupId);

        if (project.TroopId.HasValue)
            membersQuery = membersQuery.Where(m => m.TroopId == project.TroopId.Value);

        var members = await membersQuery.OrderBy(m => m.LastName).ThenBy(m => m.FirstName).ToListAsync();

        // Load existing scores for this project
        var scores = await _uow.ProjectScores.Query()
            .Where(s => s.ProjectId == projectId)
            .ToListAsync();
        var scoreMap = scores.ToDictionary(s => s.MemberId);

        return members.Select(m =>
        {
            scoreMap.TryGetValue(m.Id, out var score);
            double? pct = score != null && project.MaxScore > 0
                ? Math.Round((double)score.Score / (double)project.MaxScore * 100, 1)
                : null;
            return new ProjectMemberScoreDto
            {
                MemberId    = m.Id,
                MemberName  = m.FullName,
                CustomId    = m.CustomId,
                TroopName   = m.Troop?.Name,
                Score       = score?.Score,
                Notes       = score?.Notes,
                GradedBy    = score?.GradedBy,
                GradedAt    = score?.GradedAt,
                Percentage  = pct,
                Grade       = pct.HasValue ? GetGrade(pct.Value) : null,
                GradeArabic = pct.HasValue ? GetGradeArabic(pct.Value) : null,
                IsGraded    = score != null
            };
        });
    }

    // ── Save score (upsert) ───────────────────────────────────────────────────

    public async Task<ProjectMemberScoreDto?> SaveScoreAsync(Guid projectId, Guid memberId, SaveScoreDto dto)
    {
        var project = await _uow.Projects.GetByIdAsync(projectId);
        if (project is null) return null;

        if (dto.Score < 0 || dto.Score > project.MaxScore)
            throw new ArgumentException($"Score must be between 0 and {project.MaxScore}.");

        var existing = await _uow.ProjectScores.Query()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.ProjectId == projectId && s.MemberId == memberId);

        if (existing is not null)
        {
            existing.Score     = dto.Score;
            existing.Notes     = dto.Notes?.Trim();
            existing.GradedBy  = _currentUser.Username;
            existing.GradedAt  = DateTime.UtcNow;
            existing.UpdatedAt = DateTime.UtcNow;
            existing.IsDeleted = false;
            _uow.ProjectScores.Update(existing);
        }
        else
        {
            var score = new MemberProjectScore
            {
                ProjectId = projectId,
                MemberId  = memberId,
                Score     = dto.Score,
                Notes     = dto.Notes?.Trim(),
                GradedBy  = _currentUser.Username,
                GradedAt  = DateTime.UtcNow
            };
            await _uow.ProjectScores.AddAsync(score);
        }

        await _uow.SaveChangesAsync();

        var member = await _uow.Members.GetByIdAsync(memberId);
        double? pct = project.MaxScore > 0
            ? Math.Round((double)dto.Score / (double)project.MaxScore * 100, 1)
            : null;

        return new ProjectMemberScoreDto
        {
            MemberId    = memberId,
            MemberName  = member?.FullName ?? string.Empty,
            CustomId    = member?.CustomId ?? 0,
            Score       = dto.Score,
            Notes       = dto.Notes,
            GradedBy    = _currentUser.Username,
            GradedAt    = DateTime.UtcNow,
            Percentage  = pct,
            Grade       = pct.HasValue ? GetGrade(pct.Value) : null,
            GradeArabic = pct.HasValue ? GetGradeArabic(pct.Value) : null,
            IsGraded    = true
        };
    }

    // ── Member summary ────────────────────────────────────────────────────────

    public async Task<MemberProjectSummaryDto> GetMemberSummaryAsync(Guid memberId)
    {
        var member = await _uow.Members.Query()
            .Include(m => m.Group)
            .FirstOrDefaultAsync(m => m.Id == memberId && !m.IsDeleted);

        var scores = await _uow.ProjectScores.Query()
            .Include(s => s.Project)
            .Where(s => s.MemberId == memberId && !s.Project.IsDeleted)
            .OrderByDescending(s => s.GradedAt)
            .ToListAsync();

        decimal totalScored   = scores.Sum(s => s.Score);
        decimal totalPossible = scores.Sum(s => s.Project.MaxScore);
        double successRate    = totalPossible > 0
            ? Math.Round((double)totalScored / (double)totalPossible * 100, 1)
            : 0;

        return new MemberProjectSummaryDto
        {
            MemberId           = memberId,
            MemberName         = member?.FullName ?? string.Empty,
            TotalScored        = totalScored,
            TotalPossible      = totalPossible,
            SuccessRate        = successRate,
            OverallGrade       = totalPossible > 0 ? GetGrade(successRate)        : "—",
            OverallGradeArabic = totalPossible > 0 ? GetGradeArabic(successRate) : "—",
            ProjectCount       = scores.Count,
            Projects           = scores.Select(s =>
            {
                double pct = s.Project.MaxScore > 0
                    ? Math.Round((double)s.Score / (double)s.Project.MaxScore * 100, 1)
                    : 0;
                return new MemberProjectScoreDto
                {
                    ProjectId   = s.ProjectId,
                    ProjectName = s.Project.Name,
                    MaxScore    = s.Project.MaxScore,
                    Score       = s.Score,
                    Notes       = s.Notes,
                    GradedBy    = s.GradedBy,
                    GradedAt    = s.GradedAt,
                    Percentage  = pct,
                    Grade       = GetGrade(pct),
                    GradeArabic = GetGradeArabic(pct)
                };
            }).ToList()
        };
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    public static string GetGrade(double pct) => pct switch
    {
        >= 90 => "A",
        >= 75 => "B",
        >= 60 => "C",
        >= 50 => "D",
        _     => "F"
    };

    public static string GetGradeArabic(double pct) => pct switch
    {
        >= 90 => "ممتاز",
        >= 75 => "جيد جداً",
        >= 60 => "جيد",
        >= 50 => "مقبول",
        _     => "راسب"
    };

    private static ProjectDto MapProject(Project p) => new()
    {
        Id          = p.Id,
        Name        = p.Name,
        Description = p.Description,
        MaxScore    = p.MaxScore,
        GroupId     = p.GroupId,
        GroupName   = p.Group?.Name   ?? string.Empty,
        TroopId     = p.TroopId,
        TroopName   = p.Troop?.Name,
        CreatedBy   = p.CreatedBy,
        IsActive    = p.IsActive,
        GradedCount = p.Scores.Count(s => !s.IsDeleted),
        CreatedAt   = p.CreatedAt
    };
}
