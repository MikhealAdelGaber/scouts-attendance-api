using Microsoft.EntityFrameworkCore;
using ScoutsAttendance.Application.Common;
using ScoutsAttendance.Application.DTOs.Members;
using ScoutsAttendance.Application.Interfaces;
using ScoutsAttendance.Domain.Constants;

namespace ScoutsAttendance.Application.Services;

public interface IMemberService
{
    Task<PagedResult<MemberDto>> GetAllAsync(
        Guid? groupId, Guid? troopId,
        int page, int pageSize,
        string? search        = null,
        string? academicYear  = null,
        string? region        = null,
        bool?   hasNeckerchief = null,
        bool?   unassigned    = null);   // true = members with no troop (TroopId IS NULL)

    Task<MemberDto?> GetByIdAsync(Guid id);
    Task<MemberDto>  CreateAsync(CreateMemberDto dto);
    Task<MemberDto?> UpdateAsync(Guid id, UpdateMemberDto dto);
    Task<bool>       DeleteAsync(Guid id);
    Task<byte[]>     GetQrCodeImageAsync(Guid id);
    Task<MemberDto?> GetByQrCodeAsync(string qrToken);
    Task<int>        BulkYearUpdateAsync(BulkYearUpdateDto dto);
}

public class MemberService : IMemberService
{
    private readonly IUnitOfWork         _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly IQrCodeService      _qrCode;
    private readonly ICustomIdService    _customId;

    public MemberService(IUnitOfWork uow, ICurrentUserService currentUser, IQrCodeService qrCode, ICustomIdService customId)
    {
        _uow         = uow;
        _currentUser = currentUser;
        _qrCode      = qrCode;
        _customId    = customId;
    }

    public async Task<PagedResult<MemberDto>> GetAllAsync(
        Guid? groupId, Guid? troopId,
        int page, int pageSize,
        string? search        = null,
        string? academicYear  = null,
        string? region        = null,
        bool?   hasNeckerchief = null,
        bool?   unassigned    = null)
    {
        // IgnoreQueryFilters() prevents the global soft-delete filter on Troop
        // (and other included entities) from being injected into the JOIN condition.
        // Without it, EF Core adds "AND t.IsDeleted = FALSE" to the Troop join,
        // which causes members whose troop was soft-deleted to be silently excluded
        // from results (the join produces no row for them).
        // We re-apply the Member soft-delete filter manually below.
        // MapToDto already guards MemberPoints/Excuses with !p.IsDeleted checks.
        var query = _uow.Members.Query()
            .IgnoreQueryFilters()
            .Include(m => m.Troop)
            .Include(m => m.Group)
            .Include(m => m.MemberPoints)
            .Include(m => m.Excuses)
            .Where(m => !m.IsDeleted);

        // ── Scoping ──────────────────────────────────────────────────────────
        if (unassigned == true)
        {
            // Show only members with no troop (unassigned after troop deletion)
            query = query.Where(m => m.TroopId == null);
            // Still scope by group if the caller is not a system admin
            var gid = groupId ?? (_currentUser.IsSystemAdmin ? null : _currentUser.GroupId);
            if (gid.HasValue) query = query.Where(m => m.GroupId == gid.Value);
        }
        else
        {
            // Troop scope is most restrictive — takes priority over group filter.
            // Prefer a caller-supplied troopId; fall back to the current user's
            // JWT-embedded TroopId (HasTroopScope).
            var effectiveTroopId = troopId ?? (_currentUser.HasTroopScope ? _currentUser.TroopId : null);

            if (effectiveTroopId.HasValue)
            {
                // Guard against stale JWT claims.  A user's JWT carries their TroopId
                // at login time and is never re-issued mid-session.  If their troop
                // was deleted since then, HasTroopScope is still true but the troop
                // no longer exists.  Filtering by a deleted troop's ID would return
                // zero rows (members now have TroopId = null), making them appear
                // "deleted" in the UI.  The global query filter on Troops already
                // excludes soft-deleted rows, so AnyAsync returns false for deleted troops.
                var troopStillActive = await _uow.Troops.AnyAsync(t => t.Id == effectiveTroopId.Value);
                if (troopStillActive)
                {
                    query = query.Where(m => m.TroopId == effectiveTroopId.Value);
                }
                else
                {
                    // Troop was deleted — fall back to group-level visibility so the
                    // user can still see the now-unassigned members.
                    var gid = groupId ?? (_currentUser.IsSystemAdmin ? null : _currentUser.GroupId);
                    if (gid.HasValue) query = query.Where(m => m.GroupId == gid.Value);
                }
            }
            else
            {
                // Any non-admin user without a troop assignment is scoped to their group
                var effectiveGroupId = groupId ?? (_currentUser.IsSystemAdmin ? null : _currentUser.GroupId);
                if (effectiveGroupId.HasValue) query = query.Where(m => m.GroupId == effectiveGroupId.Value);
            }
        }

        // ── Filters ──────────────────────────────────────────────────────────
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(m =>
                m.FirstName.Contains(search) || m.LastName.Contains(search) ||
                (m.PhoneNumber != null && m.PhoneNumber.Contains(search)) ||
                m.CustomId.ToString().Contains(search));

        if (!string.IsNullOrWhiteSpace(academicYear))
            query = query.Where(m => m.AcademicYear == academicYear);

        if (!string.IsNullOrWhiteSpace(region))
            query = query.Where(m => m.Region != null && m.Region.Contains(region));

        if (hasNeckerchief.HasValue)
            query = query.Where(m => m.HasNeckerchief == hasNeckerchief.Value);

        // ── Paging ───────────────────────────────────────────────────────────
        var total = await query.CountAsync();
        var items = await query
            .OrderBy(m => m.LastName).ThenBy(m => m.FirstName)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync();

        return PagedResult<MemberDto>.Create(items.Select(MapToDto), total, page, pageSize);
    }

    public async Task<MemberDto?> GetByIdAsync(Guid id)
    {
        var m = await _uow.Members.Query()
            .IgnoreQueryFilters()
            .Include(m => m.Troop)
            .Include(m => m.Group)
            .Include(m => m.MemberPoints)
            .Include(m => m.Excuses)
            .FirstOrDefaultAsync(m => m.Id == id && !m.IsDeleted);

        return m is null ? null : MapToDto(m);
    }

    public async Task<MemberDto> CreateAsync(CreateMemberDto dto)
    {
        if (!AcademicGrades.IsValid(dto.AcademicYear))
            throw new ArgumentException(
                $"'{dto.AcademicYear}' is not a valid academic grade. Use one of the allowed values.");

        var troop = await _uow.Troops.GetByIdAsync(dto.TroopId)
            ?? throw new InvalidOperationException("Troop not found");

        var customId = await _customId.GenerateAsync(dto.Gender);

        var member = new Domain.Entities.Member
        {
            FirstName      = dto.FirstName,
            LastName       = dto.LastName,
            PhoneNumber    = dto.PhoneNumber,
            DateOfBirth    = dto.DateOfBirth,
            TroopId        = dto.TroopId,
            GroupId        = troop.GroupId,
            UserId         = dto.UserId,
            Gender         = dto.Gender,
            CustomId       = customId,
            Address        = dto.Address,
            Region         = dto.Region,
            HasNeckerchief = dto.HasNeckerchief,
            YearJoined     = dto.YearJoined,
            AcademicYear   = dto.AcademicYear,
            FatherPhone    = dto.FatherPhone,
            MotherPhone    = dto.MotherPhone,
            Notes          = dto.Notes
        };
        // QR encodes the CustomId string (e.g. "SCOUT-100001")
        member.QrCode = _qrCode.GenerateQrCodeToken(customId);

        await _uow.Members.AddAsync(member);
        await _uow.SaveChangesAsync();
        return await GetByIdAsync(member.Id) ?? throw new InvalidOperationException("Failed to retrieve created member");
    }

    public async Task<MemberDto?> UpdateAsync(Guid id, UpdateMemberDto dto)
    {
        if (!AcademicGrades.IsValid(dto.AcademicYear))
            throw new ArgumentException(
                $"'{dto.AcademicYear}' is not a valid academic grade. Use one of the allowed values.");

        var member = await _uow.Members.GetByIdAsync(id);
        if (member is null) return null;

        member.FirstName      = dto.FirstName;
        member.LastName       = dto.LastName;
        member.PhoneNumber    = dto.PhoneNumber;
        member.DateOfBirth    = dto.DateOfBirth;
        member.Gender         = dto.Gender;
        member.Address        = dto.Address;
        member.Region         = dto.Region;
        member.HasNeckerchief = dto.HasNeckerchief;
        member.YearJoined     = dto.YearJoined;
        member.AcademicYear   = dto.AcademicYear;
        member.FatherPhone    = dto.FatherPhone;
        member.MotherPhone    = dto.MotherPhone;
        member.Notes          = dto.Notes;
        member.UpdatedAt      = DateTime.UtcNow;

        if (dto.TroopId.HasValue && dto.TroopId.Value != member.TroopId.GetValueOrDefault())
        {
            var troop = await _uow.Troops.GetByIdAsync(dto.TroopId.Value);
            if (troop != null) { member.TroopId = troop.Id; member.GroupId = troop.GroupId; }
        }

        _uow.Members.Update(member);
        await _uow.SaveChangesAsync();
        return await GetByIdAsync(id);
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var member = await _uow.Members.GetByIdAsync(id);
        if (member is null) return false;
        _uow.Members.SoftDelete(member);
        await _uow.SaveChangesAsync();
        return true;
    }

    public async Task<byte[]> GetQrCodeImageAsync(Guid id)
    {
        var member = await _uow.Members.GetByIdAsync(id)
            ?? throw new KeyNotFoundException("Member not found");
        return _qrCode.GenerateQrCodeImage(member.QrCode);
    }

    public async Task<MemberDto?> GetByQrCodeAsync(string qrToken)
    {
        var customId = _qrCode.DecodeQrToken(qrToken);
        if (!customId.HasValue) return null;

        var member = await _uow.Members.Query()
            .IgnoreQueryFilters()
            .Include(m => m.Troop)
            .Include(m => m.Group)
            .Include(m => m.MemberPoints)
            .Include(m => m.Excuses)
            .FirstOrDefaultAsync(m => m.CustomId == customId.Value && !m.IsDeleted);

        return member is null ? null : MapToDto(member);
    }

    /// <summary>Bulk update academic year / grade for all members in a troop at year start.</summary>
    public async Task<int> BulkYearUpdateAsync(BulkYearUpdateDto dto)
    {
        var members = await _uow.Members.Query()
            .Where(m => m.TroopId == dto.TroopId && !m.IsDeleted)
            .ToListAsync();

        foreach (var m in members)
        {
            if (dto.AcademicYear != null) m.AcademicYear = dto.AcademicYear;
            if (dto.AdvanceGrade && m.YearJoined.HasValue) m.YearJoined++;
            m.UpdatedAt = DateTime.UtcNow;
            _uow.Members.Update(m);
        }

        await _uow.SaveChangesAsync();
        return members.Count;
    }

    private static MemberDto MapToDto(Domain.Entities.Member m) => new()
    {
        Id             = m.Id,
        CustomId       = m.CustomId,
        Gender         = m.Gender,
        FirstName      = m.FirstName,
        LastName       = m.LastName,
        FullName       = m.FullName,
        PhoneNumber    = m.PhoneNumber,
        DateOfBirth    = m.DateOfBirth,
        TroopId        = m.TroopId,                             // null when unassigned
        TroopName      = m.Troop?.Name,                         // null when unassigned
        GroupId        = m.GroupId,
        GroupName      = m.Group?.Name ?? string.Empty,
        QrCode         = m.QrCode,
        TotalPoints    = m.MemberPoints?.Where(p => !p.IsDeleted).Sum(p => p.Points) ?? 0,
        CreatedAt      = m.CreatedAt,
        Address        = m.Address,
        Region         = m.Region,
        HasNeckerchief = m.HasNeckerchief,
        YearJoined     = m.YearJoined,
        AcademicYear   = m.AcademicYear,
        FatherPhone    = m.FatherPhone,
        MotherPhone    = m.MotherPhone,
        Notes           = m.Notes,
        ProfileImageUrl = m.ProfileImageUrl,
        HasActiveExcuse = m.Excuses?.Any(e => !e.IsDeleted && e.IsActive &&
                              e.StartDate.Date <= DateTime.UtcNow.Date &&
                              (e.EndDate == null || e.EndDate.Value.Date >= DateTime.UtcNow.Date)) ?? false
    };
}
