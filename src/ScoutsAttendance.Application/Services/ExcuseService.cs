using Microsoft.EntityFrameworkCore;
using ScoutsAttendance.Application.DTOs.Excuses;
using ScoutsAttendance.Application.Interfaces;
using ScoutsAttendance.Domain.Entities;

namespace ScoutsAttendance.Application.Services;

public interface IExcuseService
{
    Task<IEnumerable<MemberExcuseDto>> GetByMemberAsync(Guid memberId);
    Task<IEnumerable<MemberExcuseDto>> GetAllActiveAsync(Guid? troopId = null);
    Task<MemberExcuseDto>  GrantAsync(GrantExcuseDto dto);
    Task<MemberExcuseDto?> UpdateAsync(Guid id, UpdateExcuseDto dto);
    Task<bool>             RevokeAsync(Guid id);
}

public class ExcuseService : IExcuseService
{
    private readonly IUnitOfWork         _uow;
    private readonly ICurrentUserService _currentUser;

    public ExcuseService(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow         = uow;
        _currentUser = currentUser;
    }

    public async Task<IEnumerable<MemberExcuseDto>> GetByMemberAsync(Guid memberId)
    {
        var excuses = await _uow.MemberExcuses.Query()
            .Include(e => e.Member)
            .Where(e => e.MemberId == memberId && !e.IsDeleted)
            .OrderByDescending(e => e.StartDate)
            .ToListAsync();

        var usernames = await GetUsernamesAsync(excuses.Select(e => e.GrantedBy));
        return excuses.Select(e => MapToDto(e, usernames));
    }

    public async Task<IEnumerable<MemberExcuseDto>> GetAllActiveAsync(Guid? troopId = null)
    {
        // Compare against today's UTC date only (midnight) so that excuses stored
        // as UTC midnight are not excluded by a same-day check at e.g. 23:59 UTC.
        var today = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc);
        var query = _uow.MemberExcuses.Query()
            .Include(e => e.Member).ThenInclude(m => m.Troop)
            .Where(e => !e.IsDeleted && e.IsActive &&
                        e.StartDate <= today && (e.EndDate == null || e.EndDate >= today));

        if (troopId.HasValue)
            query = query.Where(e => e.Member.TroopId == troopId.Value);
        else if (_currentUser.HasTroopScope)
            query = query.Where(e => e.Member.TroopId == _currentUser.TroopId!.Value);
        else if (!_currentUser.IsSystemAdmin && _currentUser.GroupId.HasValue)
            query = query.Where(e => e.Member.GroupId == _currentUser.GroupId.Value);

        var list = await query.OrderBy(e => e.Member.LastName).ToListAsync();
        var usernames = await GetUsernamesAsync(list.Select(e => e.GrantedBy));
        return list.Select(e => MapToDto(e, usernames));
    }

    public async Task<MemberExcuseDto> GrantAsync(GrantExcuseDto dto)
    {
        var member = await _uow.Members.GetByIdAsync(dto.MemberId)
            ?? throw new KeyNotFoundException("Member not found");

        // Normalise dates to UTC midnight so Npgsql's "timestamp with time zone"
        // column accepts them regardless of whether ASP.NET Core deserialised them
        // as Local or Unspecified kind.  We use the DATE portion only — time is
        // irrelevant for excuse coverage checks.
        var startUtc = DateTime.SpecifyKind(dto.StartDate.Date, DateTimeKind.Utc);
        var endUtc   = dto.EndDate.HasValue
                       ? DateTime.SpecifyKind(dto.EndDate.Value.Date, DateTimeKind.Utc)
                       : (DateTime?)null;

        var excuse = new MemberExcuse
        {
            MemberId  = dto.MemberId,
            StartDate = startUtc,
            EndDate   = endUtc,
            Reason    = dto.Reason,
            IsActive  = true,
            GrantedBy = _currentUser.UserId
        };

        await _uow.MemberExcuses.AddAsync(excuse);
        await _uow.SaveChangesAsync();

        excuse.Member = member;
        var usernames = await GetUsernamesAsync([_currentUser.UserId]);
        return MapToDto(excuse, usernames);
    }

    public async Task<MemberExcuseDto?> UpdateAsync(Guid id, UpdateExcuseDto dto)
    {
        var excuse = await _uow.MemberExcuses.Query()
            .Include(e => e.Member)
            .FirstOrDefaultAsync(e => e.Id == id && !e.IsDeleted);
        if (excuse is null) return null;

        var endUtc = dto.EndDate.HasValue
                     ? DateTime.SpecifyKind(dto.EndDate.Value.Date, DateTimeKind.Utc)
                     : (DateTime?)null;

        excuse.EndDate   = endUtc;
        excuse.IsActive  = dto.IsActive;
        excuse.Reason    = dto.Reason;
        excuse.UpdatedAt = DateTime.UtcNow;

        _uow.MemberExcuses.Update(excuse);
        await _uow.SaveChangesAsync();

        var usernames = await GetUsernamesAsync([excuse.GrantedBy]);
        return MapToDto(excuse, usernames);
    }

    public async Task<bool> RevokeAsync(Guid id)
    {
        var excuse = await _uow.MemberExcuses.GetByIdAsync(id);
        if (excuse is null) return false;
        excuse.IsActive  = false;
        excuse.UpdatedAt = DateTime.UtcNow;
        _uow.MemberExcuses.Update(excuse);
        await _uow.SaveChangesAsync();
        return true;
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────

    private async Task<Dictionary<Guid, string>> GetUsernamesAsync(IEnumerable<Guid> ids)
    {
        var uniqueIds = ids.Distinct().ToList();
        if (uniqueIds.Count == 0) return [];

        var users = await _uow.Users.Query()
            .Where(u => uniqueIds.Contains(u.Id))
            .Select(u => new { u.Id, u.Username })
            .ToListAsync();

        return users.ToDictionary(u => u.Id, u => u.Username);
    }

    private static MemberExcuseDto MapToDto(MemberExcuse e, Dictionary<Guid, string> usernames) => new()
    {
        Id               = e.Id,
        MemberId         = e.MemberId,
        MemberName       = e.Member?.FullName ?? string.Empty,
        StartDate        = e.StartDate,
        EndDate          = e.EndDate,
        Reason           = e.Reason ?? string.Empty,
        IsActive         = e.IsActive,
        CreatedAt        = e.CreatedAt,
        CreatedByUsername = usernames.TryGetValue(e.GrantedBy, out var name) ? name : "Unknown"
    };
}
