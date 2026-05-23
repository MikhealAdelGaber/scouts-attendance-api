using System.ComponentModel.DataAnnotations;

namespace ScoutsAttendance.Application.DTOs.Troops;

public class TroopDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid GroupId { get; set; }
    public string GroupName { get; set; } = string.Empty;
    public Guid? LeaderId { get; set; }
    public string? LeaderName { get; set; }
    public int      MemberCount  { get; set; }
    public decimal  TotalPoints  { get; set; }
    public DateTime CreatedAt    { get; set; }
    public string   ShareToken   { get; set; } = string.Empty;
}

public class CreateTroopDto
{
    [Required] public string Name { get; set; } = string.Empty;
    [Required] public Guid GroupId { get; set; }
    public Guid? LeaderId { get; set; }
}

public class UpdateTroopDto
{
    [Required] public string Name { get; set; } = string.Empty;
    public Guid? LeaderId { get; set; }
}
