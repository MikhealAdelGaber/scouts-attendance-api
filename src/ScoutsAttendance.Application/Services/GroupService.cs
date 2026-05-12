using Microsoft.EntityFrameworkCore;
using ScoutsAttendance.Application.DTOs.Groups;
using ScoutsAttendance.Application.Interfaces;

namespace ScoutsAttendance.Application.Services;

public interface IGroupService
{
    Task<IEnumerable<GroupDto>> GetAllAsync();
    Task<GroupDto?> GetByIdAsync(Guid id);
    Task<GroupDto> CreateAsync(CreateGroupDto dto);
    Task<GroupDto?> UpdateAsync(Guid id, UpdateGroupDto dto);
    Task<bool> DeleteAsync(Guid id);
}

public class GroupService : IGroupService
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;

    public GroupService(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<IEnumerable<GroupDto>> GetAllAsync()
    {
        var query = _uow.Groups.Query()
            .Include(g => g.Leader)
            .Include(g => g.Troops)
            .Include(g => g.Members)
            .Where(g => !g.IsDeleted);

        if (!_currentUser.IsSystemAdmin && _currentUser.GroupId.HasValue)
            query = query.Where(g => g.Id == _currentUser.GroupId.Value);

        var groups = await query.ToListAsync();
        return groups.Select(g => new GroupDto
        {
            Id = g.Id,
            Name = g.Name,
            Description = g.Description,
            LeaderId = g.LeaderId,
            LeaderName = g.Leader?.Username ?? string.Empty,
            TroopCount = g.Troops.Count(t => !t.IsDeleted),
            MemberCount = g.Members.Count(m => !m.IsDeleted),
            CreatedAt = g.CreatedAt
        });
    }

    public async Task<GroupDto?> GetByIdAsync(Guid id)
    {
        var g = await _uow.Groups.Query()
            .Include(g => g.Leader)
            .Include(g => g.Troops)
            .Include(g => g.Members)
            .FirstOrDefaultAsync(g => g.Id == id && !g.IsDeleted);

        if (g is null) return null;
        return new GroupDto
        {
            Id = g.Id,
            Name = g.Name,
            Description = g.Description,
            LeaderId = g.LeaderId,
            LeaderName = g.Leader?.Username ?? string.Empty,
            TroopCount = g.Troops.Count(t => !t.IsDeleted),
            MemberCount = g.Members.Count(m => !m.IsDeleted),
            CreatedAt = g.CreatedAt
        };
    }

    public async Task<GroupDto> CreateAsync(CreateGroupDto dto)
    {
        var group = new Domain.Entities.Group
        {
            Name = dto.Name,
            Description = dto.Description,
            LeaderId = dto.LeaderId
        };

        await _uow.Groups.AddAsync(group);
        await _uow.SaveChangesAsync();

        var leader = await _uow.Users.GetByIdAsync(dto.LeaderId)
            ?? throw new KeyNotFoundException("Leader user not found");
        leader.GroupId = group.Id;
        _uow.Users.Update(leader);
        await _uow.SaveChangesAsync();

        return new GroupDto { Id = group.Id, Name = group.Name, Description = group.Description, LeaderId = group.LeaderId, CreatedAt = group.CreatedAt };
    }

    public async Task<GroupDto?> UpdateAsync(Guid id, UpdateGroupDto dto)
    {
        var group = await _uow.Groups.GetByIdAsync(id);
        if (group is null) return null;

        group.Name = dto.Name;
        group.Description = dto.Description;
        if (dto.LeaderId.HasValue) group.LeaderId = dto.LeaderId.Value;
        group.UpdatedAt = DateTime.UtcNow;

        _uow.Groups.Update(group);
        await _uow.SaveChangesAsync();
        return await GetByIdAsync(id);
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var group = await _uow.Groups.GetByIdAsync(id);
        if (group is null) return false;

        _uow.Groups.SoftDelete(group);
        await _uow.SaveChangesAsync();
        return true;
    }
}
