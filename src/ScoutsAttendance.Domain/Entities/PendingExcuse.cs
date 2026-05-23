using ScoutsAttendance.Domain.Common;
using ScoutsAttendance.Domain.Enums;

namespace ScoutsAttendance.Domain.Entities;

/// <summary>
/// An excuse submission made by a member (or their parent) via the troop's
/// public shareable link, before it has been approved by a leader.
/// </summary>
public class PendingExcuse : BaseEntity
{
    /// <summary>The troop whose shareable link was used to submit this excuse.</summary>
    public Guid   TroopId     { get; set; }

    /// <summary>Submitter's name (e.g. parent or member name).</summary>
    public string SubmitterName { get; set; } = string.Empty;

    /// <summary>Member name (as typed by the submitter).</summary>
    public string MemberName  { get; set; } = string.Empty;

    /// <summary>Optional member CustomId (scout ID) to help match an existing member record.</summary>
    public int?   MemberCustomId { get; set; }

    /// <summary>Start date of the excuse period.</summary>
    public DateTime StartDate  { get; set; }

    /// <summary>End date of the excuse period (inclusive).</summary>
    public DateTime EndDate    { get; set; }

    /// <summary>Reason provided by the submitter.</summary>
    public string Reason      { get; set; } = string.Empty;

    /// <summary>IP address of the submitter (stored for rate-limiting audit).</summary>
    public string SubmitterIp { get; set; } = string.Empty;

    /// <summary>Current approval status of this pending excuse.</summary>
    public PendingExcuseStatus Status { get; set; } = PendingExcuseStatus.Pending;

    /// <summary>Notes added by the approver/rejecter (optional).</summary>
    public string? ReviewNotes { get; set; }

    /// <summary>The user who reviewed this excuse (null while still Pending).</summary>
    public Guid? ReviewedBy { get; set; }

    /// <summary>When the excuse was reviewed (null while still Pending).</summary>
    public DateTime? ReviewedAt { get; set; }

    /// <summary>
    /// When approved, the resulting MemberExcuse that was created.
    /// Null while Pending or Rejected.
    /// </summary>
    public Guid? ResultingExcuseId { get; set; }

    // Navigation
    public Troop Troop { get; set; } = null!;
}
