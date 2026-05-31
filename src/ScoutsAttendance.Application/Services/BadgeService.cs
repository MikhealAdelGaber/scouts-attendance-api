using Microsoft.EntityFrameworkCore;
using ScoutsAttendance.Application.DTOs.Badges;
using ScoutsAttendance.Application.Interfaces;
using ScoutsAttendance.Domain.Entities;

namespace ScoutsAttendance.Application.Services;

// ─── Interface ────────────────────────────────────────────────────────────────

public interface IBadgeService
{
    // Catalog
    Task<IEnumerable<BadgeDto>> GetAllBadgesAsync();
    Task<BadgeDto?>             GetBadgeByIdAsync(Guid id);
    Task<BadgeDto>              CreateBadgeAsync(CreateBadgeDto dto);
    Task<BadgeDto?>             UpdateBadgeAsync(Guid id, UpdateBadgeDto dto);
    Task<(bool Ok, string Error)> DeleteBadgeAsync(Guid id);

    // Member badges
    Task<IEnumerable<MemberBadgeDto>> GetMemberBadgesAsync(Guid memberId);
    Task<MemberBadgeDto>              AwardBadgeAsync(Guid memberId, AwardBadgeDto dto);
    Task<bool>                        RemoveMemberBadgeAsync(Guid memberId, Guid memberBadgeId);

    // Activity feed — recent awards in the current user's group
    Task<IEnumerable<MemberBadgeDto>> GetRecentBadgesAsync(int limit = 30);
}

// ─── Implementation ───────────────────────────────────────────────────────────

public class BadgeService : IBadgeService
{
    private readonly IUnitOfWork         _uow;
    private readonly ICurrentUserService _currentUser;

    public BadgeService(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow         = uow;
        _currentUser = currentUser;
    }

    // ── Catalog ───────────────────────────────────────────────────────────────

    public async Task<IEnumerable<BadgeDto>> GetAllBadgesAsync()
    {
        var badges = await _uow.Badges.Query()
            .Include(b => b.MemberBadges.Where(mb => !mb.IsDeleted))
            .Where(b => !b.IsDeleted)
            .OrderBy(b => b.Category)
            .ThenBy(b => b.Name)
            .ToListAsync();

        return badges.Select(MapBadgeToDto);
    }

    public async Task<BadgeDto?> GetBadgeByIdAsync(Guid id)
    {
        var badge = await _uow.Badges.Query()
            .Include(b => b.MemberBadges.Where(mb => !mb.IsDeleted))
            .FirstOrDefaultAsync(b => b.Id == id && !b.IsDeleted);
        return badge is null ? null : MapBadgeToDto(badge);
    }

    public async Task<BadgeDto> CreateBadgeAsync(CreateBadgeDto dto)
    {
        var badge = new Badge
        {
            Name        = dto.Name.Trim(),
            Description = dto.Description?.Trim(),
            Category    = dto.Category?.Trim()
        };
        await _uow.Badges.AddAsync(badge);
        await _uow.SaveChangesAsync();
        return MapBadgeToDto(badge);
    }

    public async Task<BadgeDto?> UpdateBadgeAsync(Guid id, UpdateBadgeDto dto)
    {
        var badge = await _uow.Badges.GetByIdAsync(id);
        if (badge is null || badge.IsDeleted) return null;

        badge.Name        = dto.Name.Trim();
        badge.Description = dto.Description?.Trim();
        badge.Category    = dto.Category?.Trim();
        badge.UpdatedAt   = DateTime.UtcNow;

        _uow.Badges.Update(badge);
        await _uow.SaveChangesAsync();
        return MapBadgeToDto(badge);
    }

    public async Task<(bool Ok, string Error)> DeleteBadgeAsync(Guid id)
    {
        var badge = await _uow.Badges.Query()
            .Include(b => b.MemberBadges.Where(mb => !mb.IsDeleted))
            .FirstOrDefaultAsync(b => b.Id == id && !b.IsDeleted);

        if (badge is null) return (false, "Badge not found");

        if (badge.MemberBadges.Count > 0)
            return (false, "Cannot delete a badge that has been awarded to members. Remove all awards first.");

        _uow.Badges.SoftDelete(badge);
        await _uow.SaveChangesAsync();
        return (true, string.Empty);
    }

    // ── Member Badges ──────────────────────────────────────────────────────────

    public async Task<IEnumerable<MemberBadgeDto>> GetMemberBadgesAsync(Guid memberId)
    {
        var records = await _uow.MemberBadges.Query()
            .Include(mb => mb.Badge)
            .Include(mb => mb.Member)
            .Include(mb => mb.Troop)
            .Where(mb => mb.MemberId == memberId && !mb.IsDeleted)
            .OrderByDescending(mb => mb.AwardedDate)
            .ToListAsync();

        return records.Select(MapMemberBadgeToDto);
    }

    public async Task<MemberBadgeDto> AwardBadgeAsync(Guid memberId, AwardBadgeDto dto)
    {
        // Verify member exists
        var member = await _uow.Members.GetByIdAsync(memberId)
            ?? throw new KeyNotFoundException($"Member {memberId} not found");

        // Verify badge exists
        var badge = await _uow.Badges.GetByIdAsync(dto.BadgeId)
            ?? throw new KeyNotFoundException($"Badge {dto.BadgeId} not found");

        // Resolve troop name snapshot — persist it as a string so it survives
        // troop deletion or member transfers with no data loss.
        string? troopNameSnapshot = null;
        if (_currentUser.TroopId.HasValue)
        {
            var troop = await _uow.Troops.GetByIdAsync(_currentUser.TroopId.Value);
            troopNameSnapshot = troop?.Name;
        }

        var record = new MemberBadge
        {
            MemberId    = memberId,
            BadgeId     = dto.BadgeId,
            AwardedDate = DateTime.SpecifyKind(dto.AwardedDate.Date, DateTimeKind.Utc),
            TroopId     = _currentUser.TroopId,
            TroopName   = troopNameSnapshot,
            AwardedBy   = _currentUser.Username ?? "unknown",
            Notes       = dto.Notes?.Trim()
        };

        await _uow.MemberBadges.AddAsync(record);
        await _uow.SaveChangesAsync();

        // Reload with includes for the response DTO
        var loaded = await _uow.MemberBadges.Query()
            .Include(mb => mb.Badge)
            .Include(mb => mb.Member)
            .Include(mb => mb.Troop)
            .FirstAsync(mb => mb.Id == record.Id);

        return MapMemberBadgeToDto(loaded);
    }

    public async Task<bool> RemoveMemberBadgeAsync(Guid memberId, Guid memberBadgeId)
    {
        var record = await _uow.MemberBadges.FindSingleAsync(
            mb => mb.Id == memberBadgeId && mb.MemberId == memberId && !mb.IsDeleted);

        if (record is null) return false;

        _uow.MemberBadges.SoftDelete(record);
        await _uow.SaveChangesAsync();
        return true;
    }

    public async Task<IEnumerable<MemberBadgeDto>> GetRecentBadgesAsync(int limit = 30)
    {
        var query = _uow.MemberBadges.Query()
            .Include(mb => mb.Badge)
            .Include(mb => mb.Member)
            .Where(mb => !mb.IsDeleted && !mb.Member.IsDeleted);

        // Scope to the current user's group when not SystemAdmin
        if (!_currentUser.IsSystemAdmin && _currentUser.GroupId.HasValue)
        {
            var groupId = _currentUser.GroupId.Value;
            query = query.Where(mb => mb.Member.GroupId == groupId);
        }

        var records = await query
            .OrderByDescending(mb => mb.AwardedDate)
            .ThenByDescending(mb => mb.CreatedAt)
            .Take(limit)
            .ToListAsync();

        return records.Select(MapMemberBadgeToDto);
    }

    // ── Mappers ───────────────────────────────────────────────────────────────

    private static BadgeDto MapBadgeToDto(Badge b) => new()
    {
        Id          = b.Id,
        Name        = b.Name,
        Description = b.Description,
        Category    = b.Category,
        CreatedAt   = b.CreatedAt,
        AwardCount  = b.MemberBadges?.Count ?? 0
    };

    private static MemberBadgeDto MapMemberBadgeToDto(MemberBadge mb) => new()
    {
        Id            = mb.Id,
        MemberId      = mb.MemberId,
        MemberName    = mb.Member?.FullName ?? string.Empty,
        BadgeId       = mb.BadgeId,
        BadgeName     = mb.Badge?.Name ?? string.Empty,
        BadgeCategory = mb.Badge?.Category,
        AwardedDate   = mb.AwardedDate,
        TroopId       = mb.TroopId,
        // Prefer the stored snapshot (immutable history); fall back to live nav for older records
        TroopName     = mb.TroopName ?? mb.Troop?.Name,
        AwardedBy     = mb.AwardedBy,
        Notes         = mb.Notes
    };
}
