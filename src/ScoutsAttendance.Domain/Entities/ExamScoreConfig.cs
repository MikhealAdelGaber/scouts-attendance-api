using ScoutsAttendance.Domain.Common;

namespace ScoutsAttendance.Domain.Entities;

/// <summary>
/// Per-group, per-year configuration for the exam score max marks.
/// Set once by the GroupLeader before entering scores.
/// </summary>
public class ExamScoreConfig : BaseEntity
{
    public Guid    GroupId             { get; set; }
    public int     Year               { get; set; }
    public decimal TheoreticalMaxScore { get; set; } = 50m;
    public decimal PracticalMaxScore   { get; set; } = 50m;
    public string  CreatedBy          { get; set; } = string.Empty;

    public Group Group { get; set; } = null!;
}
