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

    public async Task<IEnumerable<ExamScoreDto>> GetByMemberAsync(Guid memberId)
    {
        var list = await _uow.MemberExamScores.Query()
            .Include(x => x.Member).ThenInclude(m => m.Troop)
            .Where(x => x.MemberId == memberId && !x.IsDeleted)
            .OrderByDescending(x => x.Year)
            .ToListAsync();
        return list.Select(MapToDto);
    }

    public async Task<IEnumerable<ExamScoreDto>> GetByTroopAsync(Guid troopId, int? year = null)
    {
        var query = _uow.MemberExamScores.Query()
            .Include(x => x.Member).ThenInclude(m => m.Troop)
            .Where(x => x.Member.TroopId == troopId && !x.IsDeleted);

        if (year.HasValue) query = query.Where(x => x.Year == year.Value);

        var list = await query.OrderBy(x => x.Member.LastName).ThenBy(x => x.Year).ToListAsync();
        return list.Select(MapToDto);
    }

    public async Task<ExamScoreDto?> GetByIdAsync(Guid id)
    {
        var x = await _uow.MemberExamScores.Query()
            .Include(x => x.Member).ThenInclude(m => m.Troop)
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
        return x is null ? null : MapToDto(x);
    }

    public async Task<ExamScoreDto> CreateAsync(CreateExamScoreDto dto)
    {
        // Overwrite if same member+year already exists
        var existing = await _uow.MemberExamScores.Query()
            .FirstOrDefaultAsync(x => x.MemberId == dto.MemberId && x.Year == dto.Year && !x.IsDeleted);

        if (existing is not null)
        {
            existing.Score     = dto.Score;
            existing.Notes     = dto.Notes;
            existing.UpdatedAt = DateTime.UtcNow;
            _uow.MemberExamScores.Update(existing);
            await _uow.SaveChangesAsync();
            return (await GetByIdAsync(existing.Id))!;
        }

        var score = new MemberExamScore
        {
            MemberId  = dto.MemberId,
            Year      = dto.Year,
            Score     = dto.Score,
            Notes     = dto.Notes,
            CreatedBy = _currentUser.UserId
        };
        await _uow.MemberExamScores.AddAsync(score);
        await _uow.SaveChangesAsync();
        return (await GetByIdAsync(score.Id))!;
    }

    public async Task<ExamScoreDto?> UpdateAsync(Guid id, UpdateExamScoreDto dto)
    {
        var score = await _uow.MemberExamScores.GetByIdAsync(id);
        if (score is null) return null;
        score.Score     = dto.Score;
        score.Notes     = dto.Notes;
        score.UpdatedAt = DateTime.UtcNow;
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

    private static ExamScoreDto MapToDto(MemberExamScore x) => new()
    {
        Id         = x.Id,
        MemberId   = x.MemberId,
        MemberName = x.Member?.FullName ?? string.Empty,
        TroopName  = x.Member?.Troop?.Name ?? string.Empty,
        Year       = x.Year,
        Score      = x.Score,
        Notes      = x.Notes,
        CreatedAt  = x.CreatedAt
    };
}
