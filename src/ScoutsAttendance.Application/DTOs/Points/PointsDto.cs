using System.ComponentModel.DataAnnotations;

namespace ScoutsAttendance.Application.DTOs.Points;

public class PointCategoryDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsGlobal { get; set; }
    public decimal AttendancePresentPoints { get; set; }
    public decimal AttendanceLatePoints { get; set; }
}

public class CreatePointCategoryDto
{
    [Required] public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsGlobal { get; set; } = false;
    public decimal AttendancePresentPoints { get; set; } = 1m;
    public decimal AttendanceLatePoints { get; set; } = 0.5m;
}

public class MemberPointsDto
{
    public Guid Id { get; set; }
    public Guid MemberId { get; set; }
    public string MemberName { get; set; } = string.Empty;
    public Guid CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public decimal Points { get; set; }
    public DateTime Date { get; set; }
    public string? Note { get; set; }
    public bool IsAutomatic { get; set; }
}

public class AddMemberPointsDto
{
    [Required] public Guid MemberId { get; set; }
    [Required] public Guid CategoryId { get; set; }
    /// <summary>Positive = award, negative = deduct. Cannot be 0.</summary>
    [Required, Range(-10000, 10000)] public decimal Points { get; set; }
    public string? Note { get; set; }
    public DateTime? Date { get; set; }
}

public class TroopPointsDto
{
    public Guid Id { get; set; }
    public Guid TroopId { get; set; }
    public string TroopName { get; set; } = string.Empty;
    public Guid CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public decimal Points { get; set; }
    public DateTime Date { get; set; }
    public string? Note { get; set; }
}

public class AddTroopPointsDto
{
    [Required] public Guid TroopId { get; set; }
    [Required] public Guid CategoryId { get; set; }
    /// <summary>Positive = award, negative = deduct. Cannot be 0.</summary>
    [Required, Range(-10000, 10000)] public decimal Points { get; set; }
    public string? Note { get; set; }
    public DateTime? Date { get; set; }
}

public class MemberPointsSummaryDto
{
    public Guid MemberId { get; set; }
    public string MemberName { get; set; } = string.Empty;
    public string TroopName { get; set; } = string.Empty;
    public decimal TotalPoints { get; set; }
    public List<MemberPointsDto> History { get; set; } = [];
    public Dictionary<string, decimal> ByCategory { get; set; } = [];
}

public class TroopPointsSummaryDto
{
    public Guid TroopId { get; set; }
    public string TroopName { get; set; } = string.Empty;
    public decimal TotalPoints { get; set; }
    public decimal MemberContributionPoints { get; set; }
    public decimal ManualPoints { get; set; }
    public List<TroopPointsDto> History { get; set; } = [];
    public List<MemberContributionDto> MemberContributions { get; set; } = [];
}

public class MemberContributionDto
{
    public Guid MemberId { get; set; }
    public string MemberName { get; set; } = string.Empty;
    public decimal Points { get; set; }
}

/// <summary>Update the auto-award values for the Attendance member-point category.</summary>
public class UpdateAttendancePointsDto
{
    [Range(0, 1000)] public decimal AttendancePresentPoints { get; set; } = 1m;
    [Range(0, 1000)] public decimal AttendanceLatePoints    { get; set; } = 0.5m;
}

/// <summary>Award the same points to ALL members of a troop simultaneously.</summary>
public class AddBulkMemberPointsDto
{
    [Required] public Guid TroopId    { get; set; }
    [Required] public Guid CategoryId { get; set; }
    [Required, Range(-10000, 10000)] public decimal Points { get; set; }
    public string?   Note { get; set; }
    public DateTime? Date { get; set; }
}
