using ScoutsAttendance.Domain.Common;

namespace ScoutsAttendance.Domain.Entities;

/// <summary>
/// Immutable snapshot created at transfer-approval time.
/// Captures the member's accumulated stats in their OLD group so the data
/// is never lost even after attendance and points are reset.
/// </summary>
public class MemberTransferArchive : BaseEntity
{
    public Guid     MemberId              { get; set; }
    public string   MemberName            { get; set; } = string.Empty;

    public Guid     FromGroupId           { get; set; }
    public string   FromGroupName         { get; set; } = string.Empty;

    public Guid     ToGroupId             { get; set; }
    public string   ToGroupName           { get; set; } = string.Empty;

    /// <summary>UTC time the transfer was approved (= reset time).</summary>
    public DateTime TransferDate          { get; set; } = DateTime.UtcNow;

    /// <summary>Sum of all MemberPoints.Points at the moment of transfer.</summary>
    public decimal  TotalPointsAtTransfer { get; set; }

    /// <summary>Total number of AttendanceRecord rows for this member in the old group.</summary>
    public int      TotalAttendanceCount  { get; set; }

    /// <summary>Attendance records where status was Present, Late, or TooLate.</summary>
    public int      TotalEventsAttended   { get; set; }

    /// <summary>Number of MemberExcuse rows at the moment of transfer.</summary>
    public int      TotalExcusesCount     { get; set; }

    public DateTime ArchivedAt            { get; set; } = DateTime.UtcNow;
}
