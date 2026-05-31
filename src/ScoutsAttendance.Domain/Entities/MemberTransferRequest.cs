using ScoutsAttendance.Domain.Common;
using ScoutsAttendance.Domain.Enums;

namespace ScoutsAttendance.Domain.Entities;

/// <summary>
/// A request to transfer a member from one group to another.
/// All names are stored as string snapshots at request time so history
/// is preserved even if groups/members are later renamed or deleted.
/// </summary>
public class MemberTransferRequest : BaseEntity
{
    // ── Subject ────────────────────────────────────────────────────────────────
    public Guid   MemberId   { get; set; }
    public string MemberName { get; set; } = string.Empty;   // snapshot

    // ── Groups ─────────────────────────────────────────────────────────────────
    public Guid   FromGroupId   { get; set; }
    public string FromGroupName { get; set; } = string.Empty; // snapshot
    public Guid   ToGroupId     { get; set; }
    public string ToGroupName   { get; set; } = string.Empty; // snapshot

    // ── Requester ─────────────────────────────────────────────────────────────
    public string    RequestedBy  { get; set; } = string.Empty; // username snapshot
    public DateTime  RequestedAt  { get; set; } = DateTime.UtcNow;

    // ── Status ─────────────────────────────────────────────────────────────────
    public TransferRequestStatus Status { get; set; } = TransferRequestStatus.Pending;

    // ── Review ─────────────────────────────────────────────────────────────────
    public string?   ReviewedBy       { get; set; }   // username snapshot
    public DateTime? ReviewedAt       { get; set; }
    public string?   RejectionReason  { get; set; }

    // ── Optional free text ─────────────────────────────────────────────────────
    public string? Notes { get; set; }

    // ── Navigation ─────────────────────────────────────────────────────────────
    public Member Member    { get; set; } = null!;
    public Group  FromGroup { get; set; } = null!;
    public Group  ToGroup   { get; set; } = null!;
}
