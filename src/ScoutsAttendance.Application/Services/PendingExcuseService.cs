using Microsoft.EntityFrameworkCore;
using ScoutsAttendance.Application.DTOs.Excuses;
using ScoutsAttendance.Application.DTOs.PendingExcuses;
using ScoutsAttendance.Application.Interfaces;
using ScoutsAttendance.Domain.Entities;
using ScoutsAttendance.Domain.Enums;

namespace ScoutsAttendance.Application.Services;

public interface IPendingExcuseService
{
    // ── Public (no auth) ───────────────────────────────────────────────────
    Task<PublicTroopInfoDto?> GetTroopByTokenAsync(string token);
    Task<PendingExcuseDto>    SubmitAsync(string token, SubmitPendingExcuseDto dto, string submitterIp);

    // ── Admin / GroupLeader ────────────────────────────────────────────────
    Task<IEnumerable<PendingExcuseDto>> GetPendingAsync(Guid? troopId = null);
    Task<int>             GetPendingCountAsync(Guid? groupId = null);
    Task<PendingExcuseDto?> ReviewAsync(Guid id, ReviewPendingExcuseDto dto);
}

public class PendingExcuseService : IPendingExcuseService
{
    private readonly IUnitOfWork         _uow;
    private readonly ICurrentUserService _currentUser;

    public PendingExcuseService(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow         = uow;
        _currentUser = currentUser;
    }

    // ─── Public ──────────────────────────────────────────────────────────────

    public async Task<PublicTroopInfoDto?> GetTroopByTokenAsync(string token)
    {
        var troop = await _uow.Troops.Query()
            .IgnoreQueryFilters()
            .Include(t => t.Group)
            .FirstOrDefaultAsync(t => t.ShareToken == token && !t.IsDeleted);

        if (troop is null) return null;

        return new PublicTroopInfoDto
        {
            Id        = troop.Id,
            Name      = troop.Name,
            GroupName = troop.Group?.Name ?? string.Empty
        };
    }

    public async Task<PendingExcuseDto> SubmitAsync(string token, SubmitPendingExcuseDto dto, string submitterIp)
    {
        var troop = await _uow.Troops.Query()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.ShareToken == token && !t.IsDeleted)
            ?? throw new KeyNotFoundException("Invalid or expired excuse link.");

        if (dto.EndDate < dto.StartDate)
            throw new ArgumentException("End date must be on or after start date.");

        var pending = new PendingExcuse
        {
            TroopId        = troop.Id,
            SubmitterName  = dto.SubmitterName.Trim(),
            MemberName     = dto.MemberName.Trim(),
            MemberCustomId = dto.MemberCustomId,
            StartDate      = DateTime.SpecifyKind(dto.StartDate.Date, DateTimeKind.Utc),
            EndDate        = DateTime.SpecifyKind(dto.EndDate.Date, DateTimeKind.Utc),
            Reason         = dto.Reason.Trim(),
            SubmitterIp    = submitterIp,
            Status         = PendingExcuseStatus.Pending
        };

        await _uow.PendingExcuses.AddAsync(pending);
        await _uow.SaveChangesAsync();

        return MapToDto(pending, troop.Name);
    }

    // ─── Admin / Leader ──────────────────────────────────────────────────────

    public async Task<IEnumerable<PendingExcuseDto>> GetPendingAsync(Guid? troopId = null)
    {
        var query = _uow.PendingExcuses.Query()
            .Include(p => p.Troop)
            .Where(p => !p.IsDeleted && p.Status == PendingExcuseStatus.Pending);

        // Scope by group for non-SystemAdmin
        if (!_currentUser.IsSystemAdmin && _currentUser.GroupId.HasValue)
        {
            var groupId = _currentUser.GroupId.Value;
            query = query.Where(p => p.Troop.GroupId == groupId);
        }

        if (troopId.HasValue)
            query = query.Where(p => p.TroopId == troopId.Value);

        var list = await query.OrderByDescending(p => p.CreatedAt).ToListAsync();
        return list.Select(p => MapToDto(p, p.Troop?.Name ?? string.Empty));
    }

    public async Task<int> GetPendingCountAsync(Guid? groupId = null)
    {
        var query = _uow.PendingExcuses.Query()
            .Include(p => p.Troop)
            .Where(p => !p.IsDeleted && p.Status == PendingExcuseStatus.Pending);

        if (groupId.HasValue)
            query = query.Where(p => p.Troop.GroupId == groupId.Value);

        return await query.CountAsync();
    }

    public async Task<PendingExcuseDto?> ReviewAsync(Guid id, ReviewPendingExcuseDto dto)
    {
        var pending = await _uow.PendingExcuses.Query()
            .Include(p => p.Troop)
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);

        if (pending is null) return null;

        if (pending.Status != PendingExcuseStatus.Pending)
            throw new InvalidOperationException("This excuse has already been reviewed.");

        pending.Status      = dto.Approve ? PendingExcuseStatus.Approved : PendingExcuseStatus.Rejected;
        pending.ReviewNotes = dto.ReviewNotes;
        pending.ReviewedBy  = _currentUser.UserId;
        pending.ReviewedAt  = DateTime.UtcNow;
        pending.UpdatedAt   = DateTime.UtcNow;

        // When approving, create the actual MemberExcuse record
        if (dto.Approve)
        {
            // Resolve the member: prefer explicit MemberId, otherwise try matching by CustomId
            Guid? memberId = dto.MemberId;

            if (!memberId.HasValue && pending.MemberCustomId.HasValue)
            {
                var member = await _uow.Members.FindSingleAsync(
                    m => m.CustomId == pending.MemberCustomId.Value
                      && m.TroopId == pending.TroopId
                      && !m.IsDeleted);
                memberId = member?.Id;
            }

            if (memberId.HasValue)
            {
                var excuse = new MemberExcuse
                {
                    MemberId  = memberId.Value,
                    StartDate = pending.StartDate,
                    EndDate   = pending.EndDate,
                    Reason    = $"[Via Submission] {pending.Reason}",
                    IsActive  = true,
                    GrantedBy = _currentUser.UserId
                };
                await _uow.MemberExcuses.AddAsync(excuse);
                await _uow.SaveChangesAsync();
                pending.ResultingExcuseId = excuse.Id;
            }
        }

        _uow.PendingExcuses.Update(pending);
        await _uow.SaveChangesAsync();

        return MapToDto(pending, pending.Troop?.Name ?? string.Empty);
    }

    // ─── Mapper ──────────────────────────────────────────────────────────────

    private static PendingExcuseDto MapToDto(PendingExcuse p, string troopName) => new()
    {
        Id               = p.Id,
        TroopId          = p.TroopId,
        TroopName        = troopName,
        SubmitterName    = p.SubmitterName,
        MemberName       = p.MemberName,
        MemberCustomId   = p.MemberCustomId,
        StartDate        = p.StartDate,
        EndDate          = p.EndDate,
        Reason           = p.Reason,
        Status           = p.Status,
        StatusName       = p.Status.ToString(),
        ReviewNotes      = p.ReviewNotes,
        ReviewedAt       = p.ReviewedAt,
        ResultingExcuseId = p.ResultingExcuseId,
        CreatedAt        = p.CreatedAt
    };
}
