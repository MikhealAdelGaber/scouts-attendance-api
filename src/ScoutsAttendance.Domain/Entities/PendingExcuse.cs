using ScoutsAttendance.Domain.Common;
using ScoutsAttendance.Domain.Enums;

namespace ScoutsAttendance.Domain.Entities;

/// <summary>
/// An excuse submission made via a troop's public shareable link,
/// awaiting review by a GroupLeader or SystemAdmin.
/// </summary>
public class PendingExcuse : BaseEntity
{
    /// <summary>The troop whose shareable link was used.</summary>
    public Guid TroopId { get; set; }

    /// <summary>
    /// The specific member this excuse is for.
    /// Selected from the troop's member list — cannot be outside this troop.
    /// </summary>
    public Guid MemberId { get; set; }

    /// <summary>Name of the person who submitted the form (parent, guardian, or scout).</summary>
    public string SubmittedByName { get; set; } = string.Empty;

    /// <summary>Start date of the absence period (UTC midnight).</summary>
    public DateTime StartDate { get; set; }

    /// <summary>End date of the absence period inclusive (UTC midnight).</summary>
    public DateTime EndDate { get; set; }

    /// <summary>Reason provided by the submitter.</summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>IP address stored for rate-limiting audit purposes.</summary>
    public string SubmitterIp { get; set; } = string.Empty;

    /// <summary>Current review status.</summary>
    public PendingExcuseStatus Status { get; set; } = PendingExcuseStatus.Pending;

    /// <summary>Optional notes added by the reviewer.</summary>
    public string? ReviewNotes { get; set; }

    /// <summary>The user who reviewed this excuse (null while Pending).</summary>
    public Guid? ReviewedBy { get; set; }

    /// <summary>When the excuse was reviewed (null while Pending).</summary>
    public DateTime? ReviewedAt { get; set; }

    /// <summary>The MemberExcuse created upon approval; null while Pending or Rejected.</summary>
    public Guid? ResultingExcuseId { get; set; }

    // ── Navigation ────────────────────────────────────────────────────────────
    public Troop  Troop  { get; set; } = null!;
    public Member Member { get; set; } = null!;
}
