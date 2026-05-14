using Microsoft.EntityFrameworkCore;
using ScoutsAttendance.Application.DTOs.Troops;
using ScoutsAttendance.Application.Interfaces;

namespace ScoutsAttendance.Application.Services;

public interface ITroopService
{
    Task<IEnumerable<TroopDto>> GetAllAsync(Guid? groupId = null);
    Task<TroopDto?> GetByIdAsync(Guid id);
    Task<TroopDto> CreateAsync(CreateTroopDto dto);
    Task<TroopDto?> UpdateAsync(Guid id, UpdateTroopDto dto);
    Task<bool> DeleteAsync(Guid id);
}

public class TroopService : ITroopService
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;

    public TroopService(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<IEnumerable<TroopDto>> GetAllAsync(Guid? groupId = null)
    {
        var query = _uow.Troops.Query()
            .Include(t => t.Group)
            .Include(t => t.Leader)
            .Include(t => t.Members)
            .Include(t => t.TroopPoints)
            .Where(t => !t.IsDeleted);

        var effectiveGroupId = groupId ?? (_currentUser.IsSystemAdmin ? null : _currentUser.GroupId);
        if (effectiveGroupId.HasValue)
            query = query.Where(t => t.GroupId == effectiveGroupId.Value);

        // Non-admin users with a TroopId can only see their own troop
        if (_currentUser.HasTroopScope)
            query = query.Where(t => t.Id == _currentUser.TroopId!.Value);

        var troops = await query.ToListAsync();
        return troops.Select(t => MapToDto(t));
    }

    public async Task<TroopDto?> GetByIdAsync(Guid id)
    {
        var t = await _uow.Troops.Query()
            .Include(t => t.Group)
            .Include(t => t.Leader)
            .Include(t => t.Members.Where(m => !m.IsDeleted))
            .Include(t => t.TroopPoints)
            .FirstOrDefaultAsync(t => t.Id == id && !t.IsDeleted);

        return t is null ? null : MapToDto(t);
    }

    public async Task<TroopDto> CreateAsync(CreateTroopDto dto)
    {
        var troop = new Domain.Entities.Troop
        {
            Name = dto.Name,
            GroupId = dto.GroupId,
            LeaderId = dto.LeaderId
        };

        await _uow.Troops.AddAsync(troop);
        await _uow.SaveChangesAsync();

        if (dto.LeaderId.HasValue)
        {
            var leader = await _uow.Users.GetByIdAsync(dto.LeaderId.Value);
            if (leader != null)
            {
                leader.TroopId = troop.Id;
                leader.GroupId = dto.GroupId;
                _uow.Users.Update(leader);
                await _uow.SaveChangesAsync();
            }
        }

        return await GetByIdAsync(troop.Id) ?? throw new InvalidOperationException("Failed to load created troop");
    }

    public async Task<TroopDto?> UpdateAsync(Guid id, UpdateTroopDto dto)
    {
        var troop = await _uow.Troops.GetByIdAsync(id);
        if (troop is null) return null;

        troop.Name = dto.Name;
        if (dto.LeaderId.HasValue) troop.LeaderId = dto.LeaderId;
        troop.UpdatedAt = DateTime.UtcNow;

        _uow.Troops.Update(troop);
        await _uow.SaveChangesAsync();
        return await GetByIdAsync(id);
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var troop = await _uow.Troops.GetByIdAsync(id);
        if (troop is null) return false;

        var now = DateTime.UtcNow;

        // Step 1a: NULL-out TroopId for every MEMBER belonging to this troop.
        //
        // Raw SQL bypasses EF change-tracking and is a single round-trip regardless
        // of member count.  It also works even if the NOT NULL constraint hasn't
        // been dropped yet on an old DB (the DbSeeder ALTER TABLE fixes that on
        // startup, but just in case).
        await _uow.UnassignMembersFromTroopAsync(id, now);

        // Step 1b: NULL-out TroopId for every USER (troop leaders, attendance-only
        // users) scoped to this troop.
        //
        // This is CRITICAL for visibility.  JWT tokens carry the TroopId claim at
        // login and are never refreshed during the session.  If we do not clear the
        // User.TroopId in the database, every affected user will continue to get
        // HasTroopScope = true with the now-deleted troop's ID.  The MemberService
        // query then adds WHERE TroopId = {old id}, which returns zero results
        // because all members were just unassigned (TroopId = null).  Members appear
        // to "disappear" even though they are still in the database.
        await _uow.UnassignUsersFromTroopAsync(id, now);

        // Step 2: Soft-delete the troop itself.
        _uow.Troops.SoftDelete(troop);
        await _uow.SaveChangesAsync();
        return true;
    }

    private static TroopDto MapToDto(Domain.Entities.Troop t) => new()
    {
        Id = t.Id,
        Name = t.Name,
        GroupId = t.GroupId,
        GroupName = t.Group?.Name ?? string.Empty,
        LeaderId = t.LeaderId,
        LeaderName = t.Leader?.Username,
        MemberCount = t.Members?.Count(m => !m.IsDeleted) ?? 0,
        TotalPoints = t.TroopPoints?.Sum(p => p.Points) ?? 0,
        CreatedAt = t.CreatedAt
    };
}
