using Microsoft.EntityFrameworkCore;
using ScoutsAttendance.Application.DTOs.Reports;
using ScoutsAttendance.Application.Interfaces;
using ScoutsAttendance.Domain.Entities;
using ScoutsAttendance.Domain.Enums;

namespace ScoutsAttendance.Application.Services;

// ─── Interface ────────────────────────────────────────────────────────────────

public interface IReportService
{
    Task<IEnumerable<ReportTemplateDto>>   GetAllAsync();
    Task<ReportTemplateDto?>               GetByIdAsync(Guid id);
    Task<ReportTemplateDto>                CreateAsync(CreateReportTemplateDto dto);
    Task<ReportTemplateDto?>               UpdateAsync(Guid id, UpdateReportTemplateDto dto);
    Task<bool>                             DeleteAsync(Guid id);

    /// <summary>Calculate final scores for all members in a template.</summary>
    Task<ReportResultsDto?>                GetResultsAsync(Guid templateId, decimal passThreshold = 50m);

    /// <summary>Get/save custom scores for a specific category.</summary>
    Task<CategoryCustomScoresDto?>         GetCustomScoresAsync(Guid templateId, Guid categoryId);
    Task<int>                              SaveCustomScoresAsync(Guid templateId, SaveCustomScoresDto dto);

    /// <summary>All template results for a specific member (member profile view).</summary>
    Task<List<MemberReportSummaryDto>>     GetMemberResultsAsync(Guid memberId);
}

// ─── Implementation ───────────────────────────────────────────────────────────

public class ReportService : IReportService
{
    private readonly IUnitOfWork         _uow;
    private readonly ICurrentUserService _currentUser;

    public ReportService(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow         = uow;
        _currentUser = currentUser;
    }

    // ── CRUD ──────────────────────────────────────────────────────────────────

    public async Task<IEnumerable<ReportTemplateDto>> GetAllAsync()
    {
        IQueryable<ReportTemplate> query = _uow.ReportTemplates.Query()
            .Include(t => t.Group)
            .Include(t => t.Troop)
            .Include(t => t.Categories);

        if (!_currentUser.IsSystemAdmin && _currentUser.GroupId.HasValue)
            query = query.Where(t => t.GroupId == _currentUser.GroupId.Value);

        var list = await query.OrderByDescending(t => t.CreatedAt).ToListAsync();
        return list.Select(MapTemplate);
    }

    public async Task<ReportTemplateDto?> GetByIdAsync(Guid id)
    {
        var t = await _uow.ReportTemplates.Query()
            .Include(t => t.Group)
            .Include(t => t.Troop)
            .Include(t => t.Categories.OrderBy(c => c.SortOrder).ThenBy(c => c.CreatedAt))
            .FirstOrDefaultAsync(t => t.Id == id && !t.IsDeleted);
        return t is null ? null : MapTemplate(t);
    }

    public async Task<ReportTemplateDto> CreateAsync(CreateReportTemplateDto dto)
    {
        ValidateWeights(dto.Categories);

        var groupId = _currentUser.GroupId
            ?? throw new InvalidOperationException("No group context — cannot create template.");

        var template = new ReportTemplate
        {
            Name      = dto.Name.Trim(),
            GroupId   = groupId,
            TroopId   = dto.TroopId,
            CreatedBy = _currentUser.Username,
            IsActive  = true
        };
        await _uow.ReportTemplates.AddAsync(template);
        await _uow.SaveChangesAsync();

        // Insert categories
        int sort = 0;
        foreach (var c in dto.Categories)
        {
            var cat = new ReportTemplateCategory
            {
                ReportTemplateId  = template.Id,
                CategoryType      = c.CategoryType,
                CategoryName      = c.CategoryName.Trim(),
                Weight            = c.Weight,
                CustomDescription = c.CustomDescription?.Trim(),
                SortOrder         = c.SortOrder > 0 ? c.SortOrder : sort++
            };
            await _uow.ReportCategories.AddAsync(cat);
        }
        await _uow.SaveChangesAsync();

        return await GetByIdAsync(template.Id) ?? throw new Exception("Failed to reload template");
    }

    public async Task<ReportTemplateDto?> UpdateAsync(Guid id, UpdateReportTemplateDto dto)
    {
        var template = await _uow.ReportTemplates.Query()
            .Include(t => t.Categories)
            .FirstOrDefaultAsync(t => t.Id == id && !t.IsDeleted);
        if (template is null) return null;

        ValidateWeights(dto.Categories);

        template.Name      = dto.Name.Trim();
        template.TroopId   = dto.TroopId;
        template.IsActive  = dto.IsActive;
        template.UpdatedAt = DateTime.UtcNow;
        _uow.ReportTemplates.Update(template);

        // Remove old categories
        foreach (var c in template.Categories.ToList())
            _uow.ReportCategories.SoftDelete(c);

        // Insert new categories
        int sort = 0;
        foreach (var c in dto.Categories)
        {
            var cat = new ReportTemplateCategory
            {
                ReportTemplateId  = template.Id,
                CategoryType      = c.CategoryType,
                CategoryName      = c.CategoryName.Trim(),
                Weight            = c.Weight,
                CustomDescription = c.CustomDescription?.Trim(),
                SortOrder         = c.SortOrder > 0 ? c.SortOrder : sort++
            };
            await _uow.ReportCategories.AddAsync(cat);
        }
        await _uow.SaveChangesAsync();

        return await GetByIdAsync(id);
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var t = await _uow.ReportTemplates.GetByIdAsync(id);
        if (t is null) return false;
        _uow.ReportTemplates.SoftDelete(t);
        await _uow.SaveChangesAsync();
        return true;
    }

    // ── Calculate results ─────────────────────────────────────────────────────

    public async Task<ReportResultsDto?> GetResultsAsync(Guid templateId, decimal passThreshold = 50m)
    {
        var template = await _uow.ReportTemplates.Query()
            .Include(t => t.Group)
            .Include(t => t.Troop)
            .Include(t => t.Categories.Where(c => !c.IsDeleted).OrderBy(c => c.SortOrder).ThenBy(c => c.CreatedAt))
            .FirstOrDefaultAsync(t => t.Id == templateId && !t.IsDeleted);

        if (template is null) return null;

        // Load members
        IQueryable<Member> mq = _uow.Members.Query()
            .Include(m => m.Troop)
            .Where(m => m.GroupId == template.GroupId);
        if (template.TroopId.HasValue)
            mq = mq.Where(m => m.TroopId == template.TroopId.Value);
        var members = await mq.OrderBy(m => m.LastName).ThenBy(m => m.FirstName).ToListAsync();
        var memberIds = members.Select(m => m.Id).ToList();

        if (memberIds.Count == 0)
            return new ReportResultsDto { Template = MapTemplate(template) };

        // Load all data in bulk
        var attRecords = await _uow.AttendanceRecords.Query()
            .Where(a => memberIds.Contains(a.MemberId))
            .Select(a => new { a.MemberId, a.Status })
            .ToListAsync();

        var attByMember = attRecords.GroupBy(a => a.MemberId).ToDictionary(
            g => g.Key,
            g => new { Total = g.Count(), Attended = g.Count(a =>
                a.Status == AttendanceStatus.Present ||
                a.Status == AttendanceStatus.Late    ||
                a.Status == AttendanceStatus.TooLate) });

        var pointSums = await _uow.MemberPoints.Query()
            .Where(p => memberIds.Contains(p.MemberId))
            .GroupBy(p => p.MemberId)
            .Select(g => new { MemberId = g.Key, Total = g.Sum(p => p.Points) })
            .ToListAsync();
        var pointsByMember = pointSums.ToDictionary(p => p.MemberId, p => p.Total);
        decimal maxGroupPoints = pointsByMember.Any() ? pointsByMember.Values.Max() : 0;

        var examsList = await _uow.MemberExamScores.Query()
            .Where(e => memberIds.Contains(e.MemberId))
            .OrderByDescending(e => e.Year)
            .ToListAsync();

        // Load exam config to compute percentage
        var groupId = template.GroupId;
        var examYears = examsList.Select(e => e.Year).Distinct().ToList();
        var examConfigs = examYears.Count > 0
            ? await _uow.ExamScoreConfigs.Query()
                .Where(c => c.GroupId == groupId && examYears.Contains(c.Year) && !c.IsDeleted)
                .ToListAsync()
            : new List<Domain.Entities.ExamScoreConfig>();
        var examConfigMap = examConfigs.ToDictionary(c => c.Year);

        var examsByMember = examsList.GroupBy(e => e.MemberId)
            .ToDictionary(g => g.Key, g =>
            {
                var latest   = g.First();
                var total    = latest.TotalScore;
                examConfigMap.TryGetValue(latest.Year, out var cfg);
                decimal max  = cfg is not null ? cfg.TheoreticalMaxScore + cfg.PracticalMaxScore : 0m;
                // Return as a 0-100 percentage; fall back to TotalScore if no config
                return max > 0 ? Math.Round(total / max * 100m, 2) : total;
            });

        var projScores = await _uow.ProjectScores.Query()
            .Include(s => s.Project)
            .Where(s => memberIds.Contains(s.MemberId) && !s.Project.IsDeleted)
            .ToListAsync();
        var projRateByMember = projScores.GroupBy(s => s.MemberId)
            .ToDictionary(g => g.Key, g => {
                decimal total = g.Sum(s => s.Score);
                decimal max   = g.Sum(s => s.Project.MaxScore);
                return max > 0 ? total / max * 100m : 0m;
            });

        var badgeCounts = await _uow.MemberBadges.Query()
            .Where(b => memberIds.Contains(b.MemberId))
            .GroupBy(b => b.MemberId)
            .Select(g => new { MemberId = g.Key, Count = g.Count() })
            .ToListAsync();
        var badgesByMember = badgeCounts.ToDictionary(b => b.MemberId, b => b.Count);
        int maxGroupBadges = badgesByMember.Any() ? badgesByMember.Values.Max() : 0;

        var customScoresList = await _uow.CustomScores.Query()
            .Where(c => memberIds.Contains(c.MemberId))
            .ToListAsync();

        var categoryList = template.Categories.OrderBy(c => c.SortOrder).ThenBy(c => c.CreatedAt).ToList();

        // Calculate per member
        var results = members.Select(member =>
        {
            attByMember.TryGetValue(member.Id, out var att);
            decimal attRate = att is { Total: > 0 }
                ? Math.Round((decimal)att.Attended / att.Total * 100m, 2) : 0m;

            decimal pointsRate = maxGroupPoints > 0 && pointsByMember.TryGetValue(member.Id, out var pts)
                ? Math.Round(pts / maxGroupPoints * 100m, 2) : 0m;

            decimal examRate = examsByMember.TryGetValue(member.Id, out var examPct) ? examPct : 0m;

            decimal projRate = projRateByMember.TryGetValue(member.Id, out var pr)
                ? Math.Round(pr, 2) : 0m;

            decimal badgeRate = maxGroupBadges > 0 && badgesByMember.TryGetValue(member.Id, out var bc)
                ? Math.Round((decimal)bc / maxGroupBadges * 100m, 2) : 0m;

            var catScores = categoryList.Select(cat =>
            {
                decimal rate = cat.CategoryType switch
                {
                    CategoryType.Attendance => attRate,
                    CategoryType.Points     => pointsRate,
                    CategoryType.ExamScore  => examRate,
                    CategoryType.Project    => projRate,
                    CategoryType.Badges     => badgeRate,
                    CategoryType.Custom     =>
                        customScoresList.FirstOrDefault(c =>
                            c.ReportTemplateCategoryId == cat.Id &&
                            c.MemberId == member.Id)?.Score ?? 0m,
                    _ => 0m
                };

                decimal contrib = Math.Round(rate * cat.Weight / 100m, 2);
                return new CategoryScoreItemDto
                {
                    CategoryId       = cat.Id,
                    RawRate          = rate,
                    ContributedScore = contrib,
                    Tooltip          = $"{cat.CategoryName}: {rate:F1}% × {cat.Weight}% = {contrib:F1} pts"
                };
            }).ToList();

            decimal finalScore = Math.Round(catScores.Sum(s => s.ContributedScore), 2);

            return new MemberReportResultDto
            {
                MemberId       = member.Id,
                MemberName     = member.FullName,
                CustomId       = member.CustomId,
                TroopName      = member.Troop?.Name,
                FinalScore     = finalScore,
                Grade          = GetGrade((double)finalScore),
                GradeArabic    = GetGradeArabic((double)finalScore),
                IsPassing      = finalScore >= passThreshold,
                CategoryScores = catScores
            };
        })
        .OrderByDescending(r => r.FinalScore)
        .ToList();

        for (int i = 0; i < results.Count; i++) results[i].Rank = i + 1;

        decimal classAvg = results.Any() ? Math.Round(results.Average(r => r.FinalScore), 2) : 0m;

        var catAverages = categoryList.Select(cat => new CategoryAverageDto
        {
            CategoryId   = cat.Id,
            CategoryName = cat.CategoryName,
            AverageScore = results.Any()
                ? Math.Round(results.Average(r =>
                    r.CategoryScores.FirstOrDefault(s => s.CategoryId == cat.Id)?.ContributedScore ?? 0), 2)
                : 0m,
            AverageRate = results.Any()
                ? Math.Round(results.Average(r =>
                    r.CategoryScores.FirstOrDefault(s => s.CategoryId == cat.Id)?.RawRate ?? 0), 2)
                : 0m
        }).ToList();

        return new ReportResultsDto
        {
            Template         = MapTemplate(template),
            Results          = results,
            PassThreshold    = passThreshold,
            PassCount        = results.Count(r => r.IsPassing),
            FailCount        = results.Count(r => !r.IsPassing),
            ClassAverage     = classAvg,
            CategoryAverages = catAverages
        };
    }

    // ── Custom scores ─────────────────────────────────────────────────────────

    public async Task<CategoryCustomScoresDto?> GetCustomScoresAsync(Guid templateId, Guid categoryId)
    {
        var template = await _uow.ReportTemplates.Query()
            .Include(t => t.Troop)
            .FirstOrDefaultAsync(t => t.Id == templateId && !t.IsDeleted);
        if (template is null) return null;

        var category = await _uow.ReportCategories.Query()
            .FirstOrDefaultAsync(c => c.Id == categoryId && c.ReportTemplateId == templateId && !c.IsDeleted);
        if (category is null) return null;

        // Load all members in scope
        IQueryable<Member> mq = _uow.Members.Query()
            .Include(m => m.Troop)
            .Where(m => m.GroupId == template.GroupId);
        if (template.TroopId.HasValue)
            mq = mq.Where(m => m.TroopId == template.TroopId.Value);
        var members = await mq.OrderBy(m => m.LastName).ThenBy(m => m.FirstName).ToListAsync();

        var existingScores = await _uow.CustomScores.Query()
            .Where(c => c.ReportTemplateCategoryId == categoryId)
            .ToListAsync();
        var scoreMap = existingScores.ToDictionary(c => c.MemberId);

        return new CategoryCustomScoresDto
        {
            CategoryId   = categoryId,
            CategoryName = category.CategoryName,
            MemberScores = members.Select(m =>
            {
                scoreMap.TryGetValue(m.Id, out var cs);
                return new MemberCustomScoreReadDto
                {
                    MemberId   = m.Id,
                    MemberName = m.FullName,
                    CustomId   = m.CustomId,
                    TroopName  = m.Troop?.Name,
                    Score      = cs?.Score ?? 0m,
                    Notes      = cs?.Notes,
                    EnteredBy  = cs?.EnteredBy
                };
            }).ToList()
        };
    }

    public async Task<int> SaveCustomScoresAsync(Guid templateId, SaveCustomScoresDto dto)
    {
        // Validate category belongs to template
        var category = await _uow.ReportCategories.Query()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.Id == dto.CategoryId &&
                                      c.ReportTemplateId == templateId &&
                                      c.CategoryType == CategoryType.Custom);
        if (category is null) throw new InvalidOperationException("Category not found or not a Custom type.");

        int saved = 0;
        foreach (var item in dto.Scores)
        {
            if (item.Score < 0 || item.Score > 100)
                throw new ArgumentException($"Score for member {item.MemberId} must be 0-100.");

            var existing = await _uow.CustomScores.Query()
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(c =>
                    c.ReportTemplateCategoryId == dto.CategoryId &&
                    c.MemberId == item.MemberId);

            if (existing is not null)
            {
                existing.Score     = item.Score;
                existing.Notes     = item.Notes?.Trim();
                existing.EnteredBy = _currentUser.Username;
                existing.EnteredAt = DateTime.UtcNow;
                existing.UpdatedAt = DateTime.UtcNow;
                existing.IsDeleted = false;
                _uow.CustomScores.Update(existing);
            }
            else
            {
                await _uow.CustomScores.AddAsync(new MemberCustomScore
                {
                    ReportTemplateCategoryId = dto.CategoryId,
                    MemberId  = item.MemberId,
                    Score     = item.Score,
                    Notes     = item.Notes?.Trim(),
                    EnteredBy = _currentUser.Username,
                    EnteredAt = DateTime.UtcNow
                });
            }
            saved++;
        }
        await _uow.SaveChangesAsync();
        return saved;
    }

    // ── Member profile view ───────────────────────────────────────────────────

    public async Task<List<MemberReportSummaryDto>> GetMemberResultsAsync(Guid memberId)
    {
        var member = await _uow.Members.Query()
            .FirstOrDefaultAsync(m => m.Id == memberId && !m.IsDeleted);
        if (member is null) return [];

        var templates = await _uow.ReportTemplates.Query()
            .Include(t => t.Categories.Where(c => !c.IsDeleted).OrderBy(c => c.SortOrder))
            .Where(t => t.GroupId == member.GroupId && t.IsActive && !t.IsDeleted)
            .ToListAsync();

        var summaries = new List<MemberReportSummaryDto>();

        foreach (var tpl in templates)
        {
            // Troop scope check
            if (tpl.TroopId.HasValue && member.TroopId != tpl.TroopId.Value) continue;

            var results = await GetResultsAsync(tpl.Id);
            var mr      = results?.Results.FirstOrDefault(r => r.MemberId == memberId);
            if (mr is null) continue;

            summaries.Add(new MemberReportSummaryDto
            {
                TemplateId     = tpl.Id,
                TemplateName   = tpl.Name,
                FinalScore     = mr.FinalScore,
                Grade          = mr.Grade,
                GradeArabic    = mr.GradeArabic,
                IsPassing      = mr.IsPassing,
                CategoryScores = mr.CategoryScores,
                Categories     = tpl.Categories.Select(MapCategory).ToList()
            });
        }
        return summaries;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static void ValidateWeights(IEnumerable<CreateCategoryDto> categories)
    {
        decimal total = categories.Sum(c => c.Weight);
        if (Math.Abs(total - 100m) > 0.01m)
            throw new InvalidOperationException(
                $"Category weights must sum to 100%. Current total: {total}%");
    }

    internal static string GetGrade(double pct) => pct switch
    {
        >= 90 => "A",
        >= 75 => "B",
        >= 60 => "C",
        >= 50 => "D",
        _     => "F"
    };

    internal static string GetGradeArabic(double pct) => pct switch
    {
        >= 90 => "ممتاز",
        >= 75 => "جيد جداً",
        >= 60 => "جيد",
        >= 50 => "مقبول",
        _     => "راسب"
    };

    private static ReportTemplateDto MapTemplate(ReportTemplate t) => new()
    {
        Id          = t.Id,
        Name        = t.Name,
        GroupId     = t.GroupId,
        GroupName   = t.Group?.Name   ?? string.Empty,
        TroopId     = t.TroopId,
        TroopName   = t.Troop?.Name,
        CreatedBy   = t.CreatedBy,
        IsActive    = t.IsActive,
        TotalWeight = t.Categories.Where(c => !c.IsDeleted).Sum(c => c.Weight),
        CreatedAt   = t.CreatedAt,
        Categories  = t.Categories.Where(c => !c.IsDeleted)
                       .OrderBy(c => c.SortOrder).ThenBy(c => c.CreatedAt)
                       .Select(MapCategory).ToList()
    };

    private static ReportTemplateCategoryDto MapCategory(ReportTemplateCategory c) => new()
    {
        Id               = c.Id,
        CategoryType     = c.CategoryType,
        CategoryName     = c.CategoryName,
        Weight           = c.Weight,
        CustomDescription = c.CustomDescription,
        SortOrder        = c.SortOrder
    };
}
