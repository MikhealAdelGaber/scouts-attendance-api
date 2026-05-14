using Microsoft.EntityFrameworkCore;
using ScoutsAttendance.Application.DTOs.Points;
using ScoutsAttendance.Application.Interfaces;

namespace ScoutsAttendance.Application.Services;

public interface ILeaderboardService
{
    Task<LeaderboardDto> GetLeaderboardAsync(Guid? groupId = null);
    Task<List<TroopRankingDto>> GetTroopRankingsAsync(Guid? groupId = null);
    Task<List<MemberRankingDto>> GetMemberRankingsAsync(Guid? groupId = null, Guid? troopId = null, int top = 50);
}

public class LeaderboardService : ILeaderboardService
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;

    public LeaderboardService(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<LeaderboardDto> GetLeaderboardAsync(Guid? groupId = null)
    {
        var effectiveGroupId = ResolveGroupId(groupId);
        return new LeaderboardDto
        {
            TroopRankings = await GetTroopRankingsAsync(effectiveGroupId),
            TopMembers = await GetMemberRankingsAsync(effectiveGroupId, null, 10),
            GeneratedAt = DateTime.UtcNow
        };
    }

    public async Task<List<TroopRankingDto>> GetTroopRankingsAsync(Guid? groupId = null)
    {
        var effectiveGroupId = ResolveGroupId(groupId);

        var query = _uow.Troops.Query()
            .Include(t => t.Group)
            .Include(t => t.TroopPoints.Where(tp => !tp.IsDeleted))
            .Include(t => t.Members.Where(m => !m.IsDeleted))
                .ThenInclude(m => m.MemberPoints.Where(mp => !mp.IsDeleted))
            .Where(t => !t.IsDeleted);

        if (effectiveGroupId.HasValue)
            query = query.Where(t => t.GroupId == effectiveGroupId.Value);

        var troops = await query.ToListAsync();

        var ranked = troops
            .Select(t => new
            {
                Troop = t,
                TotalPoints = t.TroopPoints.Sum(tp => tp.Points)
                            + t.Members.Sum(m => m.MemberPoints.Sum(mp => mp.Points))
            })
            .OrderByDescending(x => x.TotalPoints)
            .Select((x, idx) => new TroopRankingDto
            {
                Rank = idx + 1,
                TroopId = x.Troop.Id,
                TroopName = x.Troop.Name,
                GroupName = x.Troop.Group?.Name ?? string.Empty,
                TotalPoints = x.TotalPoints,
                MemberCount = x.Troop.Members.Count
            })
            .ToList();

        return ranked;
    }

    public async Task<List<MemberRankingDto>> GetMemberRankingsAsync(Guid? groupId = null, Guid? troopId = null, int top = 50)
    {
        var effectiveGroupId = ResolveGroupId(groupId);

        var query = _uow.Members.Query()
            .IgnoreQueryFilters()
            .Include(m => m.Troop)
            .Include(m => m.Group)
            .Include(m => m.MemberPoints.Where(mp => !mp.IsDeleted))
                .ThenInclude(mp => mp.Category)
            .Where(m => !m.IsDeleted);

        if (effectiveGroupId.HasValue)
            query = query.Where(m => m.GroupId == effectiveGroupId.Value);

        if (troopId.HasValue)
            query = query.Where(m => m.TroopId == troopId.Value);

        var members = await query.ToListAsync();

        var ranked = members
            .Select(m => new
            {
                Member = m,
                TotalPoints = m.MemberPoints.Sum(mp => mp.Points),
                AttendancePoints = m.MemberPoints
                    .Where(mp => mp.AttendanceRecordId.HasValue)
                    .Sum(mp => mp.Points),
                BonusPoints = m.MemberPoints
                    .Where(mp => !mp.AttendanceRecordId.HasValue)
                    .Sum(mp => mp.Points)
            })
            .OrderByDescending(x => x.TotalPoints)
            .Take(top)
            .Select((x, idx) => new MemberRankingDto
            {
                Rank = idx + 1,
                MemberId = x.Member.Id,
                MemberName = x.Member.FullName,
                TroopName = x.Member.Troop?.Name ?? string.Empty,
                GroupName = x.Member.Group?.Name ?? string.Empty,
                TotalPoints = x.TotalPoints,
                AttendancePoints = x.AttendancePoints,
                BonusPoints = x.BonusPoints
            })
            .ToList();

        return ranked;
    }

    private Guid? ResolveGroupId(Guid? requested) =>
        requested ?? (_currentUser.IsSystemAdmin ? null : _currentUser.GroupId);
}
