using System.ComponentModel.DataAnnotations;

namespace ScoutsAttendance.Application.DTOs.Groups;

public class GroupDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid LeaderId { get; set; }
    public string LeaderName { get; set; } = string.Empty;
    public int TroopCount { get; set; }
    public int MemberCount { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateGroupDto
{
    [Required] public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    [Required] public Guid LeaderId { get; set; }
}

public class UpdateGroupDto
{
    [Required] public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? LeaderId { get; set; }
}
