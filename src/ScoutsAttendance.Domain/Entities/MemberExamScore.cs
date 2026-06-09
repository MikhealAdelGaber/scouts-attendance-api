using System.ComponentModel.DataAnnotations.Schema;
using ScoutsAttendance.Domain.Common;

namespace ScoutsAttendance.Domain.Entities;

/// <summary>End-of-year exam score for a scout member, split into Theoretical and Practical parts.</summary>
public class MemberExamScore : BaseEntity
{
    public Guid    MemberId           { get; set; }
    public int     Year               { get; set; }      // Scout year, e.g. 2024
    public decimal TheoreticalScore   { get; set; }      // Actual score on theory exam
    public decimal PracticalScore     { get; set; }      // Actual score on practical exam
    public string? Notes              { get; set; }
    public Guid    CreatedBy          { get; set; }

    /// <summary>Computed from TheoreticalScore + PracticalScore. Not stored in DB.</summary>
    [NotMapped]
    public decimal TotalScore => TheoreticalScore + PracticalScore;

    public Member Member { get; set; } = null!;
}
