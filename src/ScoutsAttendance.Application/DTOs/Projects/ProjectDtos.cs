using System.ComponentModel.DataAnnotations;

namespace ScoutsAttendance.Application.DTOs.Projects;

// ─── Project CRUD ─────────────────────────────────────────────────────────────

public class ProjectDto
{
    public Guid     Id          { get; set; }
    public string   Name        { get; set; } = string.Empty;
    public string?  Description { get; set; }
    public decimal  MaxScore    { get; set; }
    public Guid     GroupId     { get; set; }
    public string   GroupName   { get; set; } = string.Empty;
    public Guid?    TroopId     { get; set; }
    public string?  TroopName   { get; set; }
    public string   CreatedBy   { get; set; } = string.Empty;
    public bool     IsActive    { get; set; }
    public int      GradedCount { get; set; }
    public DateTime CreatedAt   { get; set; }
}

public class CreateProjectDto
{
    [Required, MaxLength(200)] public string   Name        { get; set; } = string.Empty;
    [MaxLength(1000)]          public string?  Description { get; set; }
    [Required, Range(1, 10000)] public decimal MaxScore    { get; set; }
    public Guid?    TroopId     { get; set; }
    /// <summary>Required for SystemAdmin (who has no automatic group). Ignored for other roles.</summary>
    public Guid?    GroupId     { get; set; }
}

public class UpdateProjectDto
{
    [Required, MaxLength(200)] public string   Name        { get; set; } = string.Empty;
    [MaxLength(1000)]          public string?  Description { get; set; }
    [Required, Range(1, 10000)] public decimal MaxScore    { get; set; }
    public Guid?    TroopId     { get; set; }
    public bool     IsActive    { get; set; } = true;
}

// ─── Grading ──────────────────────────────────────────────────────────────────

public class SaveScoreDto
{
    [Range(0, 100000)] public decimal  Score { get; set; }
    [MaxLength(500)]   public string?  Notes { get; set; }
}

/// <summary>One row in the "grade members" table — member info + current score (if any).</summary>
public class ProjectMemberScoreDto
{
    public Guid      MemberId    { get; set; }
    public string    MemberName  { get; set; } = string.Empty;
    public int       CustomId    { get; set; }
    public string?   TroopName   { get; set; }
    public decimal?  Score       { get; set; }       // null = not graded yet
    public string?   Notes       { get; set; }
    public string?   GradedBy    { get; set; }
    public DateTime? GradedAt    { get; set; }
    public double?   Percentage  { get; set; }
    public string?   Grade       { get; set; }
    public string?   GradeArabic { get; set; }
    public bool      IsGraded    { get; set; }
}

// ─── Member project history ───────────────────────────────────────────────────

public class MemberProjectScoreDto
{
    public Guid     ProjectId    { get; set; }
    public string   ProjectName  { get; set; } = string.Empty;
    public decimal  MaxScore     { get; set; }
    public decimal  Score        { get; set; }
    public string?  Notes        { get; set; }
    public string   GradedBy     { get; set; } = string.Empty;
    public DateTime GradedAt     { get; set; }
    public double   Percentage   { get; set; }
    public string   Grade        { get; set; } = string.Empty;
    public string   GradeArabic  { get; set; } = string.Empty;
}

public class MemberProjectSummaryDto
{
    public Guid     MemberId       { get; set; }
    public string   MemberName     { get; set; } = string.Empty;
    public decimal  TotalScored    { get; set; }
    public decimal  TotalPossible  { get; set; }
    public double   SuccessRate    { get; set; }
    public string   OverallGrade   { get; set; } = string.Empty;
    public string   OverallGradeArabic { get; set; } = string.Empty;
    public int      ProjectCount   { get; set; }
    public List<MemberProjectScoreDto> Projects { get; set; } = new();
}
