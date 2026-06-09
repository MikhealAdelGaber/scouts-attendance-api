using Microsoft.EntityFrameworkCore;
using ScoutsAttendance.Application.DTOs.ExamScores;
using ScoutsAttendance.Application.Interfaces;
using ScoutsAttendance.Domain.Entities;

namespace ScoutsAttendance.Application.Services;

public interface IExamScoreService
{
    Task<IEnumerable<ExamScoreDto>> GetByMemberAsync(Guid memberId);
    Task<IEnumerable<ExamScoreDto>> GetByTroopAsync(Guid troopId, int? year = null);
    Task<ExamScoreDto?>             GetByIdAsync(Guid id);
    Task<ExamScoreDto>              CreateAsync(CreateExamScoreDto dto);
    Task<ExamScoreDto?>             UpdateAsync(Guid id, UpdateExamScoreDto dto);
    Task<bool>                      DeleteAsync(Guid id);

    // ── Config ────────────────────────────────────────────────────────────────
    Task<ExamScoreConfigDto?>  GetConfigAsync(Guid groupId, int year);
    Task<ExamScoreConfigDto>   SaveConfigAsync(Guid groupId, SaveExamScoreConfigDto dto);
}

public class ExamScoreService : IExamScoreService
{
    private readonly IUnitOfWork         _uow;
    private readonly ICurrentUserService _currentUser;

    public ExamScoreService(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow         = uow;
        _currentUser = currentUser;
    }

    // ── Score CRUD ────────────────────────────────────────────────────────────

    public async Task<IEnumerable<ExamScoreDto>> GetByMemberAsync(Guid memberId)
    {
        var list = await _uow.MemberExamScores.Query()
            .Include(x => x.Member).ThenInclude(m => m.Troop).ThenInclude(t => t!.Group)
            .Where(x => x.MemberId == memberId && !x.IsDeleted)
            .OrderByDescending(x => x.Year)
            .ToListAsync();

        var groupIds = list.Select(x => x.Member?.Troop?.GroupId ?? x.Member?.GroupId ?? Guid.Empty)
                          .Distinct().ToList();
        var years = list.Select(x => x.Year).Distinct().ToList();
        var configs = await LoadConfigsAsync(groupIds, years);

        return list.Select(x => MapToDto(x, configs));
    }

    public async Task<IEnumerable<ExamScoreDto>> GetByTroopAsync(Guid troopId, int? year = null)
    {
        var query = _uow.MemberExamScores.Query()
            .Include(x => x.Member).ThenInclude(m => m.Troop)
            .Where(x => x.Member.TroopId == troopId && !x.IsDeleted);

        if (year.HasValue) query = query.Where(x => x.Year == year.Value);

        var list = await query.OrderBy(x => x.Member.LastName).ThenBy(x => x.Year).ToListAsync();

        // Load config so we can compute percentage
        var troop = await _uow.Troops.Query().FirstOrDefaultAsync(t => t.Id == troopId);
        var groupId = troop?.GroupId;
        Dictionary<(Guid, int), ExamScoreConfig> configs = [];
        if (groupId.HasValue)
        {
            var yearList = year.HasValue ? [year.Value] : list.Select(x => x.Year).Distinct().ToList();
            configs = await LoadConfigsAsync([groupId.Value], yearList);
        }

        return list.Select(x => MapToDto(x, configs));
    }

    public async Task<ExamScoreDto?> GetByIdAsync(Guid id)
    {
        var x = await _uow.MemberExamScores.Query()
            .Include(x => x.Member).ThenInclude(m => m.Troop)
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
        if (x is null) return null;

        var groupId = x.Member?.Troop?.GroupId ?? x.Member?.GroupId;
        Dictionary<(Guid, int), ExamScoreConfig> configs = [];
        if (groupId.HasValue)
            configs = await LoadConfigsAsync([groupId.Value], [x.Year]);

        return MapToDto(x, configs);
    }

    public async Task<ExamScoreDto> CreateAsync(CreateExamScoreDto dto)
    {
        // Overwrite if same member+year already exists
        var existing = await _uow.MemberExamScores.Query()
            .FirstOrDefaultAsync(x => x.MemberId == dto.MemberId && x.Year == dto.Year && !x.IsDeleted);

        if (existing is not null)
        {
            existing.TheoreticalScore = dto.TheoreticalScore;
            existing.PracticalScore   = dto.PracticalScore;
            existing.Notes            = dto.Notes;
            existing.UpdatedAt        = DateTime.UtcNow;
            _uow.MemberExamScores.Update(existing);
            await _uow.SaveChangesAsync();
            return (await GetByIdAsync(existing.Id))!;
        }

        var score = new MemberExamScore
        {
            MemberId          = dto.MemberId,
            Year              = dto.Year,
            TheoreticalScore  = dto.TheoreticalScore,
            PracticalScore    = dto.PracticalScore,
            Notes             = dto.Notes,
            CreatedBy         = _currentUser.UserId
        };
        await _uow.MemberExamScores.AddAsync(score);
        await _uow.SaveChangesAsync();
        return (await GetByIdAsync(score.Id))!;
    }

    public async Task<ExamScoreDto?> UpdateAsync(Guid id, UpdateExamScoreDto dto)
    {
        var score = await _uow.MemberExamScores.GetByIdAsync(id);
        if (score is null) return null;
        score.TheoreticalScore = dto.TheoreticalScore;
        score.PracticalScore   = dto.PracticalScore;
        score.Notes            = dto.Notes;
        score.UpdatedAt        = DateTime.UtcNow;
        _uow.MemberExamScores.Update(score);
        await _uow.SaveChangesAsync();
        return await GetByIdAsync(id);
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var score = await _uow.MemberExamScores.GetByIdAsync(id);
        if (score is null) return false;
        _uow.MemberExamScores.SoftDelete(score);
        await _uow.SaveChangesAsync();
        return true;
    }

    // ── Config ────────────────────────────────────────────────────────────────

    public async Task<ExamScoreConfigDto?> GetConfigAsync(Guid groupId, int year)
    {
        var cfg = await _uow.ExamScoreConfigs.Query()
            .FirstOrDefaultAsync(c => c.GroupId == groupId && c.Year == year && !c.IsDeleted);
        return cfg is null ? null : MapConfig(cfg);
    }

    public async Task<ExamScoreConfigDto> SaveConfigAsync(Guid groupId, SaveExamScoreConfigDto dto)
    {
        var existing = await _uow.ExamScoreConfigs.Query()
            .FirstOrDefaultAsync(c => c.GroupId == groupId && c.Year == dto.Year && !c.IsDeleted);

        if (existing is not null)
        {
            existing.TheoreticalMaxScore = dto.TheoreticalMaxScore;
            existing.PracticalMaxScore   = dto.PracticalMaxScore;
            existing.UpdatedAt           = DateTime.UtcNow;
            _uow.ExamScoreConfigs.Update(existing);
        }
        else
        {
            existing = new ExamScoreConfig
            {
                GroupId             = groupId,
                Year                = dto.Year,
                TheoreticalMaxScore = dto.TheoreticalMaxScore,
                PracticalMaxScore   = dto.PracticalMaxScore,
                CreatedBy           = _currentUser.Username
            };
            await _uow.ExamScoreConfigs.AddAsync(existing);
        }

        await _uow.SaveChangesAsync();
        return MapConfig(existing);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<Dictionary<(Guid GroupId, int Year), ExamScoreConfig>> LoadConfigsAsync(
        List<Guid> groupIds, List<int> years)
    {
        if (groupIds.Count == 0 || years.Count == 0) return [];
        var list = await _uow.ExamScoreConfigs.Query()
            .Where(c => groupIds.Contains(c.GroupId) && years.Contains(c.Year) && !c.IsDeleted)
            .ToListAsync();
        return list.ToDictionary(c => (c.GroupId, c.Year));
    }

    public static ExamScoreDto MapToDto(
        MemberExamScore x,
        Dictionary<(Guid, int), ExamScoreConfig> configs)
    {
        var groupId = x.Member?.Troop?.GroupId ?? x.Member?.GroupId ?? Guid.Empty;
        configs.TryGetValue((groupId, x.Year), out var cfg);

        decimal total    = x.TotalScore;
        decimal totalMax = cfg is not null ? cfg.TheoreticalMaxScore + cfg.PracticalMaxScore : 0m;
        decimal? pct     = totalMax > 0 ? Math.Round(total / totalMax * 100m, 2) : null;
        string? grade    = pct.HasValue ? GetGrade((double)pct.Value) : null;

        return new ExamScoreDto
        {
            Id               = x.Id,
            MemberId         = x.MemberId,
            MemberName       = x.Member?.FullName ?? string.Empty,
            TroopName        = x.Member?.Troop?.Name ?? string.Empty,
            Year             = x.Year,
            TheoreticalScore = x.TheoreticalScore,
            PracticalScore   = x.PracticalScore,
            TotalScore       = total,
            Percentage       = pct,
            Grade            = grade,
            Notes            = x.Notes,
            CreatedAt        = x.CreatedAt
        };
    }

    private static ExamScoreConfigDto MapConfig(ExamScoreConfig c) => new()
    {
        Id                  = c.Id,
        GroupId             = c.GroupId,
        Year                = c.Year,
        TheoreticalMaxScore = c.TheoreticalMaxScore,
        PracticalMaxScore   = c.PracticalMaxScore
    };

    public static string GetGrade(double pct)
    {
        if (pct >= 90) return "Excellent";
        if (pct >= 75) return "Very Good";
        if (pct >= 60) return "Good";
        if (pct >= 50) return "Pass";
        return "Fail";
    }
}
