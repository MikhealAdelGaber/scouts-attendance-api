using Microsoft.EntityFrameworkCore;
using ScoutsAttendance.Application.DTOs.TransferRequests;
using ScoutsAttendance.Application.Interfaces;
using ScoutsAttendance.Domain.Entities;
using ScoutsAttendance.Domain.Enums;

namespace ScoutsAttendance.Application.Services;

// ─── Interface ────────────────────────────────────────────────────────────────

public interface ITransferRequestService
{
    /// <summary>Create a new pending transfer request (GroupLeader or SystemAdmin).</summary>
    Task<TransferRequestDto> CreateAsync(CreateTransferRequestDto dto);

    /// <summary>
    /// List requests.  SystemAdmin sees all; GroupLeader sees requests
    /// originating from or directed at their group.
    /// </summary>
    Task<IEnumerable<TransferRequestDto>> GetListAsync();

    /// <summary>Approve + execute the transfer (SystemAdmin only).</summary>
    Task<(bool Ok, string Error)> ApproveAsync(Guid id);

    /// <summary>Previous-group archive snapshots for a member (read-only history).</summary>
    Task<IEnumerable<MemberTransferArchiveDto>> GetMemberArchiveAsync(Guid memberId);

    /// <summary>Reject with an optional reason (SystemAdmin only).</summary>
    Task<(bool Ok, string Error)> RejectAsync(Guid id, ReviewTransferRequestDto dto);

    /// <summary>Cancel a pending request (GroupLeader who created it, or SystemAdmin).</summary>
    Task<(bool Ok, string Error)> CancelAsync(Guid id);

    /// <summary>Transfer history for a specific member (approved requests only).</summary>
    Task<IEnumerable<TransferRequestDto>> GetMemberHistoryAsync(Guid memberId);

    /// <summary>Count of pending requests visible to the current user (for nav badge).</summary>
    Task<int> GetPendingCountAsync();

    /// <summary>Create transfer requests for multiple members to the same target group.</summary>
    Task<BulkTransferRequestResultDto> BulkCreateAsync(BulkCreateTransferRequestDto dto);
}

// ─── Implementation ───────────────────────────────────────────────────────────

public class TransferRequestService : ITransferRequestService
{
    private readonly IUnitOfWork         _uow;
    private readonly ICurrentUserService _currentUser;

    public TransferRequestService(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow         = uow;
        _currentUser = currentUser;
    }

    // ── Create ────────────────────────────────────────────────────────────────

    public async Task<TransferRequestDto> CreateAsync(CreateTransferRequestDto dto)
    {
        // Resolve member
        var member = await _uow.Members.Query()
            .Include(m => m.Group)
            .FirstOrDefaultAsync(m => m.Id == dto.MemberId && !m.IsDeleted)
            ?? throw new KeyNotFoundException($"Member {dto.MemberId} not found");

        // GroupLeader: can only request transfers from their own group
        if (_currentUser.IsGroupLeader && _currentUser.GroupId.HasValue)
        {
            if (member.GroupId != _currentUser.GroupId.Value)
                throw new UnauthorizedAccessException("You can only request transfers for members in your own group.");
        }

        // Resolve target group
        var toGroup = await _uow.Groups.GetByIdAsync(dto.ToGroupId)
            ?? throw new KeyNotFoundException($"Target group {dto.ToGroupId} not found");

        if (member.GroupId == dto.ToGroupId)
            throw new InvalidOperationException("The member is already in the target group.");

        // Check no existing pending request for this member
        var existingPending = await _uow.TransferRequests.Query()
            .AnyAsync(r => r.MemberId == dto.MemberId
                        && r.Status == TransferRequestStatus.Pending
                        && !r.IsDeleted);
        if (existingPending)
            throw new InvalidOperationException("A pending transfer request already exists for this member.");

        var fromGroup = member.Group ?? await _uow.Groups.GetByIdAsync(member.GroupId);

        var request = new MemberTransferRequest
        {
            MemberId        = dto.MemberId,
            MemberName      = member.FullName,
            FromGroupId     = member.GroupId,
            FromGroupName   = fromGroup?.Name ?? string.Empty,
            ToGroupId       = dto.ToGroupId,
            ToGroupName     = toGroup.Name,
            RequestedBy     = _currentUser.Username ?? "unknown",
            RequestedAt     = DateTime.UtcNow,
            Status          = TransferRequestStatus.Pending,
            Notes           = dto.Notes?.Trim()
        };

        await _uow.TransferRequests.AddAsync(request);
        await _uow.SaveChangesAsync();

        return MapToDto(request);
    }

    // ── List ──────────────────────────────────────────────────────────────────

    public async Task<IEnumerable<TransferRequestDto>> GetListAsync()
    {
        var query = _uow.TransferRequests.Query()
            .Where(r => !r.IsDeleted);

        if (!_currentUser.IsSystemAdmin && _currentUser.GroupId.HasValue)
        {
            var groupId = _currentUser.GroupId.Value;
            query = query.Where(r => r.FromGroupId == groupId || r.ToGroupId == groupId);
        }

        var list = await query
            .OrderByDescending(r => r.RequestedAt)
            .ToListAsync();

        return list.Select(MapToDto);
    }

    // ── Approve ───────────────────────────────────────────────────────────────

    public async Task<(bool Ok, string Error)> ApproveAsync(Guid id)
    {
        var request = await _uow.TransferRequests.Query()
            .FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted);

        if (request is null)
            return (false, "Transfer request not found.");
        if (request.Status != TransferRequestStatus.Pending)
            return (false, $"Request is already {request.Status.ToString().ToLower()}.");

        var member = await _uow.Members.GetByIdAsync(request.MemberId);
        if (member is null || member.IsDeleted)
            return (false, "Member no longer exists.");

        // ── Step 1: Snapshot stats BEFORE any deletion ─────────────────────────
        var totalPoints = await _uow.MemberPoints.Query()
            .Where(p => p.MemberId == member.Id)
            .SumAsync(p => (decimal?)p.Points) ?? 0m;

        var totalAttendance = await _uow.AttendanceRecords.Query()
            .CountAsync(a => a.MemberId == member.Id);

        var totalEventsAttended = await _uow.AttendanceRecords.Query()
            .CountAsync(a => a.MemberId == member.Id &&
                             (a.Status == AttendanceStatus.Present ||
                              a.Status == AttendanceStatus.Late    ||
                              a.Status == AttendanceStatus.TooLate));

        var totalExcuses = await _uow.MemberExcuses.Query()
            .CountAsync(e => e.MemberId == member.Id);

        var now = DateTime.UtcNow;

        // ── Steps 2-7: All inside a single transaction ─────────────────────────
        await _uow.ExecuteInTransactionAsync(async () =>
        {
            // 2. Archive snapshot of old-group stats
            var archive = new MemberTransferArchive
            {
                MemberId              = member.Id,
                MemberName            = member.FullName,
                FromGroupId           = request.FromGroupId,
                FromGroupName         = request.FromGroupName,
                ToGroupId             = request.ToGroupId,
                ToGroupName           = request.ToGroupName,
                TransferDate          = now,
                TotalPointsAtTransfer = totalPoints,
                TotalAttendanceCount  = totalAttendance,
                TotalEventsAttended   = totalEventsAttended,
                TotalExcusesCount     = totalExcuses,
                ArchivedAt            = now
            };
            await _uow.TransferArchives.AddAsync(archive);

            // 3. Hard-delete all MemberPoints for this member.
            //    Delete points BEFORE attendance records because of the FK
            //    MemberPoints.AttendanceRecordId → AttendanceRecords.Id.
            await _uow.DeleteMemberPointsAsync(member.Id);

            // 4. Hard-delete all AttendanceRecords for this member.
            await _uow.DeleteMemberAttendanceRecordsAsync(member.Id);

            // 5. Hard-delete all MemberExcuse records for this member.
            await _uow.DeleteMemberExcusesAsync(member.Id);

            // 6. Hard-delete all PendingExcuse requests for this member.
            await _uow.DeleteMemberPendingExcusesAsync(member.Id);

            // 8. Move member to new group and clear troop assignment.
            member.GroupId   = request.ToGroupId;
            member.TroopId   = null;
            member.UpdatedAt = now;
            _uow.Members.Update(member);

            // 9. Mark request as approved.
            request.Status     = TransferRequestStatus.Approved;
            request.ReviewedBy = _currentUser.Username ?? "unknown";
            request.ReviewedAt = now;
            request.UpdatedAt  = now;
            _uow.TransferRequests.Update(request);

            // 10. Persist all tracked changes (archive + member + request).
            await _uow.SaveChangesAsync();
        });

        return (true, string.Empty);
    }

    // ── Reject ────────────────────────────────────────────────────────────────

    public async Task<(bool Ok, string Error)> RejectAsync(Guid id, ReviewTransferRequestDto dto)
    {
        var request = await _uow.TransferRequests.Query()
            .FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted);

        if (request is null)          return (false, "Transfer request not found.");
        if (request.Status != TransferRequestStatus.Pending)
            return (false, $"Request is already {request.Status.ToString().ToLower()}.");

        request.Status          = TransferRequestStatus.Rejected;
        request.ReviewedBy      = _currentUser.Username ?? "unknown";
        request.ReviewedAt      = DateTime.UtcNow;
        request.RejectionReason = dto.RejectionReason?.Trim();
        request.UpdatedAt       = DateTime.UtcNow;

        _uow.TransferRequests.Update(request);
        await _uow.SaveChangesAsync();
        return (true, string.Empty);
    }

    // ── Cancel ────────────────────────────────────────────────────────────────

    public async Task<(bool Ok, string Error)> CancelAsync(Guid id)
    {
        var request = await _uow.TransferRequests.Query()
            .FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted);

        if (request is null)          return (false, "Transfer request not found.");
        if (request.Status != TransferRequestStatus.Pending)
            return (false, "Only pending requests can be cancelled.");

        // GroupLeader can only cancel their own group's requests
        if (_currentUser.IsGroupLeader && _currentUser.GroupId.HasValue)
        {
            if (request.FromGroupId != _currentUser.GroupId.Value)
                return (false, "You can only cancel requests from your own group.");
        }

        request.Status    = TransferRequestStatus.Cancelled;
        request.UpdatedAt = DateTime.UtcNow;

        _uow.TransferRequests.Update(request);
        await _uow.SaveChangesAsync();
        return (true, string.Empty);
    }

    // ── Archive (previous-group history) ────────────────────────────────────

    public async Task<IEnumerable<MemberTransferArchiveDto>> GetMemberArchiveAsync(Guid memberId)
    {
        var list = await _uow.TransferArchives.Query()
            .Where(a => a.MemberId == memberId)
            .OrderByDescending(a => a.TransferDate)
            .ToListAsync();

        return list.Select(a => new MemberTransferArchiveDto
        {
            Id                    = a.Id,
            MemberId              = a.MemberId,
            MemberName            = a.MemberName,
            FromGroupId           = a.FromGroupId,
            FromGroupName         = a.FromGroupName,
            ToGroupId             = a.ToGroupId,
            ToGroupName           = a.ToGroupName,
            TransferDate          = a.TransferDate,
            TotalPointsAtTransfer = a.TotalPointsAtTransfer,
            TotalAttendanceCount  = a.TotalAttendanceCount,
            TotalEventsAttended   = a.TotalEventsAttended,
            TotalExcusesCount     = a.TotalExcusesCount,
            ArchivedAt            = a.ArchivedAt
        });
    }

    // ── History ───────────────────────────────────────────────────────────────

    public async Task<IEnumerable<TransferRequestDto>> GetMemberHistoryAsync(Guid memberId)
    {
        var list = await _uow.TransferRequests.Query()
            .Where(r => r.MemberId == memberId
                     && r.Status == TransferRequestStatus.Approved
                     && !r.IsDeleted)
            .OrderByDescending(r => r.ReviewedAt)
            .ToListAsync();

        return list.Select(MapToDto);
    }

    // ── Pending count ─────────────────────────────────────────────────────────

    public async Task<int> GetPendingCountAsync()
    {
        var query = _uow.TransferRequests.Query()
            .Where(r => r.Status == TransferRequestStatus.Pending && !r.IsDeleted);

        if (!_currentUser.IsSystemAdmin && _currentUser.GroupId.HasValue)
        {
            var groupId = _currentUser.GroupId.Value;
            query = query.Where(r => r.FromGroupId == groupId || r.ToGroupId == groupId);
        }

        return await query.CountAsync();
    }

    // ── Bulk Create ───────────────────────────────────────────────────────────

    public async Task<BulkTransferRequestResultDto> BulkCreateAsync(BulkCreateTransferRequestDto dto)
    {
        // Validate target group
        var toGroup = await _uow.Groups.GetByIdAsync(dto.ToGroupId)
            ?? throw new KeyNotFoundException($"Target group {dto.ToGroupId} not found");

        // Load all requested members
        var members = await _uow.Members.Query()
            .Include(m => m.Group)
            .Where(m => dto.MemberIds.Contains(m.Id) && !m.IsDeleted)
            .ToListAsync();

        // GroupLeader: only their own group's members
        if (_currentUser.IsGroupLeader && _currentUser.GroupId.HasValue)
        {
            if (members.Any(m => m.GroupId != _currentUser.GroupId.Value))
                throw new UnauthorizedAccessException("You can only request transfers for members in your own group.");
        }

        // Members already in the target group are invalid
        members = members.Where(m => m.GroupId != dto.ToGroupId).ToList();

        // Collect memberIds that already have a pending request
        var memberIds = members.Select(m => m.Id).ToList();
        var alreadyPending = await _uow.TransferRequests.Query()
            .Where(r => memberIds.Contains(r.MemberId)
                     && r.Status == TransferRequestStatus.Pending
                     && !r.IsDeleted)
            .Select(r => r.MemberId)
            .ToListAsync();

        var pendingSet = new HashSet<Guid>(alreadyPending);
        int created = 0;
        int skipped = 0;
        var now     = DateTime.UtcNow;

        foreach (var member in members)
        {
            if (pendingSet.Contains(member.Id)) { skipped++; continue; }

            var fromGroup = member.Group ?? await _uow.Groups.GetByIdAsync(member.GroupId);
            var request   = new MemberTransferRequest
            {
                MemberId      = member.Id,
                MemberName    = member.FullName,
                FromGroupId   = member.GroupId,
                FromGroupName = fromGroup?.Name ?? string.Empty,
                ToGroupId     = dto.ToGroupId,
                ToGroupName   = toGroup.Name,
                RequestedBy   = _currentUser.Username ?? "unknown",
                RequestedAt   = now,
                Status        = TransferRequestStatus.Pending,
                Notes         = dto.Notes?.Trim()
            };
            await _uow.TransferRequests.AddAsync(request);
            created++;
        }

        await _uow.SaveChangesAsync();
        return new BulkTransferRequestResultDto { Created = created, Skipped = skipped, GroupName = toGroup.Name };
    }

    // ── Mapper ────────────────────────────────────────────────────────────────

    private static TransferRequestDto MapToDto(MemberTransferRequest r) => new()
    {
        Id              = r.Id,
        MemberId        = r.MemberId,
        MemberName      = r.MemberName,
        FromGroupId     = r.FromGroupId,
        FromGroupName   = r.FromGroupName,
        ToGroupId       = r.ToGroupId,
        ToGroupName     = r.ToGroupName,
        RequestedBy     = r.RequestedBy,
        RequestedAt     = r.RequestedAt,
        Status          = r.Status,
        StatusLabel     = r.Status.ToString(),
        ReviewedBy      = r.ReviewedBy,
        ReviewedAt      = r.ReviewedAt,
        RejectionReason = r.RejectionReason,
        Notes           = r.Notes
    };
}
