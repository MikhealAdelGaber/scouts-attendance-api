using System.ComponentModel.DataAnnotations;

namespace ScoutsAttendance.Application.DTOs.Admin;

// ─── Request ─────────────────────────────────────────────────────────────────

public class StartNewYearDto
{
    /// <summary>SystemAdmin's current password — verified server-side with BCrypt.</summary>
    [Required] public string Password    { get; set; } = string.Empty;

    /// <summary>Must be exactly "CONFIRM" (case-sensitive).</summary>
    [Required] public string ConfirmText { get; set; } = string.Empty;
}

public class VerifyPasswordDto
{
    [Required] public string Password { get; set; } = string.Empty;
}

// ─── Response ─────────────────────────────────────────────────────────────────

public class NewYearResultDto
{
    public Guid     ArchiveId      { get; set; }
    public string   ArchiveYear    { get; set; } = string.Empty;
    public int      TotalMembers   { get; set; }
    public int      TotalGroups    { get; set; }
    public DateTime ArchivedAt     { get; set; }
    public int      PointsDeleted  { get; set; }
    public int      ExcusesDeleted { get; set; }
    public int      PendingExcusesDeleted { get; set; }
}

// ─── Archive list / detail ────────────────────────────────────────────────────

public class YearlyArchiveSummaryDto
{
    public Guid     Id           { get; set; }
    public string   ArchiveYear  { get; set; } = string.Empty;
    public DateTime ArchivedAt   { get; set; }
    public string   ArchivedBy   { get; set; } = string.Empty;
    public int      TotalMembers { get; set; }
    public int      TotalGroups  { get; set; }
}

public class YearlyArchiveDetailDto : YearlyArchiveSummaryDto
{
    public List<YearlyMemberArchiveDto> Members { get; set; } = new();
}

public class YearlyMemberArchiveDto
{
    public Guid     Id                    { get; set; }
    public Guid     MemberId              { get; set; }
    public string   MemberName            { get; set; } = string.Empty;
    public Guid     GroupId               { get; set; }
    public string   GroupName             { get; set; } = string.Empty;
    public string?  TroopName             { get; set; }
    public decimal  TotalPointsAtYearEnd  { get; set; }
    public int      TotalAttendanceCount  { get; set; }
    public int      TotalEventsAttended   { get; set; }
    public int      TotalExcusesCount     { get; set; }
    public string?  AcademicGrade         { get; set; }
}
