using Microsoft.EntityFrameworkCore;
using ScoutsAttendance.Application.DTOs.Points;
using ScoutsAttendance.Application.Interfaces;
using ScoutsAttendance.Domain.Entities;

namespace ScoutsAttendance.Application.Services;

public interface IPointsService
{
    // ── Member Categories ──────────────────────────────────────────────────────
    Task<IEnumerable<PointCategoryDto>> GetMemberCategoriesAsync(Guid? groupId = null);
    Task<PointCategoryDto>              CreateMemberCategoryAsync(CreatePointCategoryDto dto);
    Task<PointCategoryDto?>             GetAttendanceCategoryAsync();
    Task<PointCategoryDto?>             UpdateAttendanceCategoryAsync(Guid id, UpdateAttendancePointsDto dto);

    /// <summary>
    /// Deletes a member point category ONLY when it has never been used (no MemberPoints reference it).
    /// Returns (true,"") on success; (false, reason) if in-use or not found.
    /// </summary>
    Task<(bool Ok, string Error)> DeleteMemberCategoryAsync(Guid id);

    // ── Troop Categories ───────────────────────────────────────────────────────
    Task<IEnumerable<PointCategoryDto>> GetTroopCategoriesAsync(Guid? groupId = null);
    Task<PointCategoryDto>              CreateTroopCategoryAsync(CreatePointCategoryDto dto);

    // ── Member Points ──────────────────────────────────────────────────────────
    Task<MemberPointsSummaryDto> GetMemberPointsAsync(Guid memberId);
    Task<MemberPointsDto>        AddMemberPointsAsync(AddMemberPointsDto dto);
    Task<bool>                   DeleteMemberPointsAsync(Guid pointsId);

    // ── Troop Points ───────────────────────────────────────────────────────────
    Task<TroopPointsSummaryDto> GetTroopPointsAsync(Guid troopId);
    Task<TroopPointsDto>        AddTroopPointsAsync(AddTroopPointsDto dto);
    Task<bool>                  DeleteTroopPointsAsync(Guid pointsId);
}

public class PointsService : IPointsService
{
    private readonly IUnitOfWork         _uow;
    private readonly ICurrentUserService _currentUser;

    public PointsService(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow         = uow;
        _currentUser = currentUser;
    }

    // ─── Member Categories ─────────────────────────────────────────────────────

    public async Task<IEnumerable<PointCategoryDto>> GetMemberCategoriesAsync(Guid? groupId = null)
    {
        var effectiveGroupId = groupId ?? _currentUser.GroupId;
        var cats = await _uow.MemberPointCategories.Query()
            .Where(c => c.IsGlobal || c.GroupId == effectiveGroupId)
            .ToListAsync();

        if (!cats.Any() && effectiveGroupId.HasValue)
            cats = await SeedDefaultMemberCategoriesAsync(effectiveGroupId.Value);

        return cats.Select(MapMemberCategory);
    }

    public async Task<PointCategoryDto> CreateMemberCategoryAsync(CreatePointCategoryDto dto)
    {
        var cat = new MemberPointCategory
        {
            Name                    = dto.Name,
            Description             = dto.Description,
            IsGlobal                = dto.IsGlobal,
            GroupId                 = dto.IsGlobal ? null : _currentUser.GroupId,
            AttendancePresentPoints = dto.AttendancePresentPoints,
            AttendanceLatePoints    = dto.AttendanceLatePoints
        };
        await _uow.MemberPointCategories.AddAsync(cat);
        await _uow.SaveChangesAsync();
        return MapMemberCategory(cat);
    }

    public async Task<(bool Ok, string Error)> DeleteMemberCategoryAsync(Guid id)
    {
        var cat = await _uow.MemberPointCategories.GetByIdAsync(id);
        if (cat is null)
            return (false, "Category not found.");

        // Prevent deletion if any MemberPoints record references this category
        bool isUsed = await _uow.MemberPoints.AnyAsync(
            p => p.MemberPointCategoryId == id && !p.IsDeleted);

        if (isUsed)
            return (false, "This category cannot be deleted because it has been used to award points. Remove the related points first.");

        _uow.MemberPointCategories.SoftDelete(cat);
        await _uow.SaveChangesAsync();
        return (true, string.Empty);
    }

    public async Task<PointCategoryDto?> GetAttendanceCategoryAsync()
    {
        var groupId = _currentUser.GroupId;
        var cat = await _uow.MemberPointCategories.FindSingleAsync(
            c => c.Name == "Attendance" && !c.IsDeleted &&
                 (c.IsGlobal || c.GroupId == groupId));

        // Auto-seed if not found yet
        if (cat is null && groupId.HasValue)
        {
            var seeded = await SeedDefaultMemberCategoriesAsync(groupId.Value);
            cat = seeded.First(c => c.Name == "Attendance");
        }

        return cat is null ? null : MapMemberCategory(cat);
    }

    public async Task<PointCategoryDto?> UpdateAttendanceCategoryAsync(Guid id, UpdateAttendancePointsDto dto)
    {
        var cat = await _uow.MemberPointCategories.GetByIdAsync(id);
        if (cat is null) return null;

        cat.AttendancePresentPoints = dto.AttendancePresentPoints;
        cat.AttendanceLatePoints    = dto.AttendanceLatePoints;
        cat.UpdatedAt               = DateTime.UtcNow;

        _uow.MemberPointCategories.Update(cat);
        await _uow.SaveChangesAsync();
        return MapMemberCategory(cat);
    }

    // ─── Troop Categories ──────────────────────────────────────────────────────

    public async Task<IEnumerable<PointCategoryDto>> GetTroopCategoriesAsync(Guid? groupId = null)
    {
        var effectiveGroupId = groupId ?? _currentUser.GroupId;
        var cats = await _uow.TroopPointCategories.Query()
            .Where(c => c.IsGlobal || c.GroupId == effectiveGroupId)
            .ToListAsync();

        if (!cats.Any() && effectiveGroupId.HasValue)
            cats = await SeedDefaultTroopCategoriesAsync(effectiveGroupId.Value);

        return cats.Select(MapTroopCategory);
    }

    public async Task<PointCategoryDto> CreateTroopCategoryAsync(CreatePointCategoryDto dto)
    {
        var cat = new TroopPointCategory
        {
            Name        = dto.Name,
            Description = dto.Description,
            IsGlobal    = dto.IsGlobal,
            GroupId     = dto.IsGlobal ? null : _currentUser.GroupId
        };
        await _uow.TroopPointCategories.AddAsync(cat);
        await _uow.SaveChangesAsync();
        return MapTroopCategory(cat);
    }

    // ─── Member Points ─────────────────────────────────────────────────────────

    public async Task<MemberPointsSummaryDto> GetMemberPointsAsync(Guid memberId)
    {
        var member = await _uow.Members.Query()
            .IgnoreQueryFilters()
            .Include(m => m.Troop)
            .Include(m => m.MemberPoints).ThenInclude(mp => mp.Category)
            .FirstOrDefaultAsync(m => m.Id == memberId && !m.IsDeleted)
            ?? throw new KeyNotFoundException("Member not found");

        var pts = member.MemberPoints.Where(mp => !mp.IsDeleted).ToList();

        return new MemberPointsSummaryDto
        {
            MemberId    = member.Id,
            MemberName  = member.FullName,
            TroopName   = member.Troop?.Name ?? string.Empty,
            TotalPoints = pts.Sum(p => p.Points),
            History = pts.OrderByDescending(p => p.Date).Select(p => new MemberPointsDto
            {
                Id           = p.Id,
                MemberId     = p.MemberId,
                MemberName   = member.FullName,
                CategoryId   = p.MemberPointCategoryId ?? Guid.Empty,
                CategoryName = p.Category?.Name ?? string.Empty,
                Points       = p.Points,
                Date         = p.Date,
                Note         = p.Note,
                IsAutomatic  = p.AttendanceRecordId.HasValue
            }).ToList(),
            ByCategory = pts
                .GroupBy(p => p.Category?.Name ?? "Uncategorised")
                .ToDictionary(g => g.Key, g => g.Sum(p => p.Points))
        };
    }

    public async Task<MemberPointsDto> AddMemberPointsAsync(AddMemberPointsDto dto)
    {
        await AuthorizeForMemberAsync(dto.MemberId);

        var member = await _uow.Members.Query()
            .IgnoreQueryFilters()
            .Include(m => m.Troop)
            .FirstOrDefaultAsync(m => m.Id == dto.MemberId && !m.IsDeleted)
            ?? throw new KeyNotFoundException("Member not found");

        var categoryId = await EnsureMemberCategoryAsync(dto.CategoryId, member.GroupId);

        var mp = new MemberPoints
        {
            MemberId              = dto.MemberId,
            MemberPointCategoryId = categoryId,
            Points                = dto.Points,
            Date                  = dto.Date ?? DateTime.UtcNow,
            Note                  = dto.Note,
            AddedBy               = _currentUser.UserId
        };
        await _uow.MemberPoints.AddAsync(mp);
        await _uow.SaveChangesAsync();

        var category = await _uow.MemberPointCategories.GetByIdAsync(categoryId ?? Guid.Empty);
        return new MemberPointsDto
        {
            Id           = mp.Id,
            MemberId     = mp.MemberId,
            MemberName   = member.FullName,
            CategoryId   = mp.MemberPointCategoryId ?? Guid.Empty,
            CategoryName = category?.Name ?? string.Empty,
            Points       = mp.Points,
            Date         = mp.Date,
            Note         = mp.Note,
            IsAutomatic  = false
        };
    }

    public async Task<bool> DeleteMemberPointsAsync(Guid pointsId)
    {
        var mp = await _uow.MemberPoints.GetByIdAsync(pointsId);
        if (mp is null) return false;
        _uow.MemberPoints.SoftDelete(mp);
        await _uow.SaveChangesAsync();
        return true;
    }

    // ─── Troop Points ──────────────────────────────────────────────────────────

    public async Task<TroopPointsSummaryDto> GetTroopPointsAsync(Guid troopId)
    {
        var troop = await _uow.Troops.Query()
            .Include(t => t.TroopPoints).ThenInclude(tp => tp.Category)
            .Include(t => t.Members.Where(m => !m.IsDeleted))
                .ThenInclude(m => m.MemberPoints.Where(mp => !mp.IsDeleted))
            .FirstOrDefaultAsync(t => t.Id == troopId && !t.IsDeleted)
            ?? throw new KeyNotFoundException("Troop not found");

        var manual  = troop.TroopPoints.Where(tp => !tp.IsDeleted).Sum(tp => tp.Points);
        var members = troop.Members.Sum(m => m.MemberPoints.Sum(mp => mp.Points));

        return new TroopPointsSummaryDto
        {
            TroopId                  = troop.Id,
            TroopName                = troop.Name,
            TotalPoints              = manual + members,
            MemberContributionPoints = members,
            ManualPoints             = manual,
            History = troop.TroopPoints.Where(tp => !tp.IsDeleted)
                .OrderByDescending(p => p.Date)
                .Select(p => new TroopPointsDto
                {
                    Id           = p.Id,
                    TroopId      = p.TroopId,
                    TroopName    = troop.Name,
                    CategoryId   = p.TroopPointCategoryId ?? Guid.Empty,
                    CategoryName = p.Category?.Name ?? string.Empty,
                    Points       = p.Points,
                    Date         = p.Date,
                    Note         = p.Note
                }).ToList(),
            MemberContributions = troop.Members
                .Select(m => new MemberContributionDto
                {
                    MemberId   = m.Id,
                    MemberName = m.FullName,
                    Points     = m.MemberPoints.Sum(mp => mp.Points)
                }).OrderByDescending(c => c.Points).ToList()
        };
    }

    public async Task<TroopPointsDto> AddTroopPointsAsync(AddTroopPointsDto dto)
    {
        await AuthorizeForTroopAsync(dto.TroopId);

        var troop = await _uow.Troops.GetByIdAsync(dto.TroopId)
            ?? throw new KeyNotFoundException("Troop not found");

        var tp = new TroopPoints
        {
            TroopId              = dto.TroopId,
            TroopPointCategoryId = dto.CategoryId == Guid.Empty ? null : dto.CategoryId,
            Points               = dto.Points,
            Date                 = dto.Date ?? DateTime.UtcNow,
            Note                 = dto.Note,
            AddedBy              = _currentUser.UserId
        };
        await _uow.TroopPoints.AddAsync(tp);
        await _uow.SaveChangesAsync();

        var category = dto.CategoryId != Guid.Empty
            ? await _uow.TroopPointCategories.GetByIdAsync(dto.CategoryId)
            : null;

        return new TroopPointsDto
        {
            Id           = tp.Id,
            TroopId      = tp.TroopId,
            TroopName    = troop.Name,
            CategoryId   = tp.TroopPointCategoryId ?? Guid.Empty,
            CategoryName = category?.Name ?? string.Empty,
            Points       = tp.Points,
            Date         = tp.Date,
            Note         = tp.Note
        };
    }

    public async Task<bool> DeleteTroopPointsAsync(Guid pointsId)
    {
        var tp = await _uow.TroopPoints.GetByIdAsync(pointsId);
        if (tp is null) return false;
        _uow.TroopPoints.SoftDelete(tp);
        await _uow.SaveChangesAsync();
        return true;
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private async Task AuthorizeForMemberAsync(Guid memberId)
    {
        if (_currentUser.IsSystemAdmin || _currentUser.IsGroupLeader) return;
        if (_currentUser.HasTroopScope)
        {
            var member = await _uow.Members.GetByIdAsync(memberId);
            if (member?.TroopId != _currentUser.TroopId)
                throw new UnauthorizedAccessException("You can only add points to members in your own troop");
        }
    }

    private Task AuthorizeForTroopAsync(Guid troopId)
    {
        if (_currentUser.IsSystemAdmin || _currentUser.IsGroupLeader) return Task.CompletedTask;
        if (_currentUser.HasTroopScope && _currentUser.TroopId != troopId)
            throw new UnauthorizedAccessException("You can only manage points for your own troop");
        return Task.CompletedTask;
    }

    /// <summary>Validates or finds a fallback MemberPointCategory for a group.</summary>
    public async Task<Guid?> EnsureMemberCategoryAsync(Guid categoryId, Guid groupId)
    {
        if (categoryId != Guid.Empty)
        {
            var exists = await _uow.MemberPointCategories.AnyAsync(c => c.Id == categoryId && !c.IsDeleted);
            if (exists) return categoryId;
        }

        var first = await _uow.MemberPointCategories.Query()
            .Where(c => c.IsGlobal || c.GroupId == groupId)
            .FirstOrDefaultAsync();
        if (first is not null) return first.Id;

        var seeded = await SeedDefaultMemberCategoriesAsync(groupId);
        return seeded.First().Id;
    }

    private async Task<List<MemberPointCategory>> SeedDefaultMemberCategoriesAsync(Guid groupId)
    {
        var defaults = new[]
        {
            new MemberPointCategory { Name = "Attendance",  Description = "Auto-awarded for attending events", GroupId = groupId, AttendancePresentPoints = 1m, AttendanceLatePoints = 0.5m },
            new MemberPointCategory { Name = "Behavior",    Description = "Points for good behavior",          GroupId = groupId },
            new MemberPointCategory { Name = "Activity",    Description = "Points for activities",             GroupId = groupId },
            new MemberPointCategory { Name = "Exam",        Description = "Points for exam performance",       GroupId = groupId },
            new MemberPointCategory { Name = "Discipline",  Description = "Points for discipline",             GroupId = groupId },
        };
        foreach (var cat in defaults) await _uow.MemberPointCategories.AddAsync(cat);
        await _uow.SaveChangesAsync();
        return [.. defaults];
    }

    private async Task<List<TroopPointCategory>> SeedDefaultTroopCategoriesAsync(Guid groupId)
    {
        var defaults = new[]
        {
            new TroopPointCategory { Name = "Competition",       Description = "Points for competition results",       GroupId = groupId },
            new TroopPointCategory { Name = "Community Service", Description = "Points for community service",         GroupId = groupId },
            new TroopPointCategory { Name = "Event Performance", Description = "Points for overall event performance", GroupId = groupId },
            new TroopPointCategory { Name = "Scout Challenge",   Description = "Points for scout challenges",          GroupId = groupId },
            new TroopPointCategory { Name = "Bonus",             Description = "Bonus points for the troop",          GroupId = groupId },
        };
        foreach (var cat in defaults) await _uow.TroopPointCategories.AddAsync(cat);
        await _uow.SaveChangesAsync();
        return [.. defaults];
    }

    private static PointCategoryDto MapMemberCategory(MemberPointCategory c) => new()
    {
        Id                      = c.Id,
        Name                    = c.Name,
        Description             = c.Description,
        IsGlobal                = c.IsGlobal,
        AttendancePresentPoints = c.AttendancePresentPoints,
        AttendanceLatePoints    = c.AttendanceLatePoints
    };

    private static PointCategoryDto MapTroopCategory(TroopPointCategory c) => new()
    {
        Id          = c.Id,
        Name        = c.Name,
        Description = c.Description,
        IsGlobal    = c.IsGlobal
    };
}
