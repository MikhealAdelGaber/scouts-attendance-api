using Microsoft.EntityFrameworkCore;
using ScoutsAttendance.Application.DTOs.PendingExcuses;
using ScoutsAttendance.Application.Interfaces;
using ScoutsAttendance.Domain.Entities;
using ScoutsAttendance.Domain.Enums;

namespace ScoutsAttendance.Application.Services;

public interface IPendingExcuseService
{
    // ── Public (no auth) ────────────────────────────────────────────────────
    Task<PublicTroopInfoDto?> GetTroopByTokenAsync(string token);
    Task<PendingExcuseDto>    SubmitAsync(string token, SubmitPendingExcuseDto dto, string submitterIp);

    // ── Admin / GroupLeader ─────────────────────────────────────────────────
    Task<IEnumerable<PendingExcuseDto>> GetPendingAsync(Guid? troopId = null);
    Task<int>             GetPendingCountAsync(Guid? groupId = null);
    Task<PendingExcuseDto?> ApproveAsync(Guid id, string? reviewNotes);
    Task<PendingExcuseDto?> RejectAsync(Guid id, string? reviewNotes);
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

    // ─── Public endpoints ────────────────────────────────────────────────────

    public async Task<PublicTroopInfoDto?> GetTroopByTokenAsync(string token)
    {
        var troop = await _uow.Troops.Query()
            .IgnoreQueryFilters()
            .Include(t => t.Group)
            .FirstOrDefaultAsync(t => t.ShareToken == token && !t.IsDeleted);

        if (troop is null) return null;

        // Load active members for this troop (name + scout ID for the dropdown)
        var members = await _uow.Members.Query()
            .Where(m => m.TroopId == troop.Id && !m.IsDeleted)
            .OrderBy(m => m.FirstName).ThenBy(m => m.LastName)
            .Select(m => new PublicMemberDto
            {
                Id       = m.Id,
                FullName = m.FirstName + " " + m.LastName,
                CustomId = m.CustomId
            })
            .ToListAsync();

        return new PublicTroopInfoDto
        {
            Id        = troop.Id,
            Name      = troop.Name,
            GroupName = troop.Group?.Name ?? string.Empty,
            Members   = members
        };
    }

    public async Task<PendingExcuseDto> SubmitAsync(string token, SubmitPendingExcuseDto dto, string submitterIp)
    {
        // Validate token
        var troop = await _uow.Troops.Query()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.ShareToken == token && !t.IsDeleted)
            ?? throw new KeyNotFoundException("Invalid or expired excuse link.");

        // Validate dates
        if (dto.EndDate < dto.StartDate)
            throw new ArgumentException("End date must be on or after start date.");

        // Validate that the chosen member actually belongs to this troop
        var member = await _uow.Members.FindSingleAsync(
            m => m.Id == dto.MemberId && m.TroopId == troop.Id && !m.IsDeleted)
            ?? throw new ArgumentException("Selected member does not belong to this troop.");

        var pending = new PendingExcuse
        {
            TroopId        = troop.Id,
            MemberId       = member.Id,
            SubmittedByName = dto.SubmittedByName.Trim(),
            StartDate      = DateTime.SpecifyKind(dto.StartDate.Date, DateTimeKind.Utc),
            EndDate        = DateTime.SpecifyKind(dto.EndDate.Date, DateTimeKind.Utc),
            Reason         = dto.Reason.Trim(),
            SubmitterIp    = submitterIp,
            Status         = PendingExcuseStatus.Pending
        };

        await _uow.PendingExcuses.AddAsync(pending);
        await _uow.SaveChangesAsync();

        return MapToDto(pending, troop.Name, member.FullName, member.CustomId);
    }

    // ─── Admin / Leader endpoints ────────────────────────────────────────────

    public async Task<IEnumerable<PendingExcuseDto>> GetPendingAsync(Guid? troopId = null)
    {
        var query = _uow.PendingExcuses.Query()
            .Include(p => p.Troop)
            .Include(p => p.Member)
            .Where(p => !p.IsDeleted && p.Status == PendingExcuseStatus.Pending);

        // Scope to this user's group unless SystemAdmin
        if (!_currentUser.IsSystemAdmin && _currentUser.GroupId.HasValue)
        {
            var groupId = _currentUser.GroupId.Value;
            query = query.Where(p => p.Troop.GroupId == groupId);
        }

        if (troopId.HasValue)
            query = query.Where(p => p.TroopId == troopId.Value);

        var list = await query.OrderByDescending(p => p.CreatedAt).ToListAsync();
        return list.Select(p => MapToDto(
            p,
            p.Troop?.Name   ?? string.Empty,
            p.Member?.FullName ?? string.Empty,
            p.Member?.CustomId ?? 0));
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

    public async Task<PendingExcuseDto?> ApproveAsync(Guid id, string? reviewNotes)
    {
        var pending = await _uow.PendingExcuses.Query()
            .Include(p => p.Troop)
            .Include(p => p.Member)
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);

        if (pending is null) return null;

        if (pending.Status != PendingExcuseStatus.Pending)
            throw new InvalidOperationException("This excuse has already been reviewed.");

        // Create the real MemberExcuse record in the main system
        var excuse = new MemberExcuse
        {
            MemberId  = pending.MemberId,
            StartDate = pending.StartDate,
            EndDate   = pending.EndDate,
            Reason    = $"[Approved] {pending.Reason}",
            IsActive  = true,
            GrantedBy = _currentUser.UserId
        };
        await _uow.MemberExcuses.AddAsync(excuse);
        await _uow.SaveChangesAsync();

        // Mark the pending excuse as Approved and link the resulting excuse
        pending.Status            = PendingExcuseStatus.Approved;
        pending.ReviewNotes       = reviewNotes;
        pending.ReviewedBy        = _currentUser.UserId;
        pending.ReviewedAt        = DateTime.UtcNow;
        pending.UpdatedAt         = DateTime.UtcNow;
        pending.ResultingExcuseId = excuse.Id;

        _uow.PendingExcuses.Update(pending);
        await _uow.SaveChangesAsync();

        return MapToDto(pending,
            pending.Troop?.Name    ?? string.Empty,
            pending.Member?.FullName ?? string.Empty,
            pending.Member?.CustomId ?? 0);
    }

    public async Task<PendingExcuseDto?> RejectAsync(Guid id, string? reviewNotes)
    {
        var pending = await _uow.PendingExcuses.Query()
            .Include(p => p.Troop)
            .Include(p => p.Member)
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);

        if (pending is null) return null;

        if (pending.Status != PendingExcuseStatus.Pending)
            throw new InvalidOperationException("This excuse has already been reviewed.");

        pending.Status      = PendingExcuseStatus.Rejected;
        pending.ReviewNotes = reviewNotes;
        pending.ReviewedBy  = _currentUser.UserId;
        pending.ReviewedAt  = DateTime.UtcNow;
        pending.UpdatedAt   = DateTime.UtcNow;

        _uow.PendingExcuses.Update(pending);
        await _uow.SaveChangesAsync();

        return MapToDto(pending,
            pending.Troop?.Name    ?? string.Empty,
            pending.Member?.FullName ?? string.Empty,
            pending.Member?.CustomId ?? 0);
    }

    // ─── Mapper ──────────────────────────────────────────────────────────────

    private static PendingExcuseDto MapToDto(
        PendingExcuse p,
        string troopName,
        string memberName,
        int memberCustomId) => new()
    {
        Id                = p.Id,
        TroopId           = p.TroopId,
        TroopName         = troopName,
        MemberId          = p.MemberId,
        MemberName        = memberName,
        MemberCustomId    = memberCustomId,
        SubmittedByName   = p.SubmittedByName,
        StartDate         = p.StartDate,
        EndDate           = p.EndDate,
        Reason            = p.Reason,
        Status            = p.Status,
        StatusName        = p.Status.ToString(),
        ReviewNotes       = p.ReviewNotes,
        ReviewedAt        = p.ReviewedAt,
        ResultingExcuseId = p.ResultingExcuseId,
        CreatedAt         = p.CreatedAt
    };
}
