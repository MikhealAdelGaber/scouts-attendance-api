using System.ComponentModel.DataAnnotations;
using ScoutsAttendance.Domain.Enums;

namespace ScoutsAttendance.Application.DTOs.PendingExcuses;

// ── Public read-only troop info (returned to anonymous submitters) ──────────
public class PublicTroopInfoDto
{
    public Guid   Id        { get; set; }
    public string Name      { get; set; } = string.Empty;
    public string GroupName { get; set; } = string.Empty;
}

// ── Submission payload sent by an anonymous user ────────────────────────────
public class SubmitPendingExcuseDto
{
    [Required, MaxLength(200)]
    public string SubmitterName { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string MemberName    { get; set; } = string.Empty;

    public int? MemberCustomId { get; set; }

    [Required]
    public DateTime StartDate   { get; set; }

    [Required]
    public DateTime EndDate     { get; set; }

    [Required, MaxLength(1000)]
    public string Reason        { get; set; } = string.Empty;
}

// ── Full DTO returned to admins/leaders ────────────────────────────────────
public class PendingExcuseDto
{
    public Guid               Id              { get; set; }
    public Guid               TroopId         { get; set; }
    public string             TroopName       { get; set; } = string.Empty;
    public string             SubmitterName   { get; set; } = string.Empty;
    public string             MemberName      { get; set; } = string.Empty;
    public int?               MemberCustomId  { get; set; }
    public DateTime           StartDate       { get; set; }
    public DateTime           EndDate         { get; set; }
    public string             Reason          { get; set; } = string.Empty;
    public PendingExcuseStatus Status         { get; set; }
    public string             StatusName      { get; set; } = string.Empty;
    public string?            ReviewNotes     { get; set; }
    public DateTime?          ReviewedAt      { get; set; }
    public Guid?              ResultingExcuseId { get; set; }
    public DateTime           CreatedAt       { get; set; }
}

// ── Review payload (approve or reject) ─────────────────────────────────────
public class ReviewPendingExcuseDto
{
    /// <summary>true = approve; false = reject</summary>
    [Required]
    public bool    Approve     { get; set; }

    [MaxLength(500)]
    public string? ReviewNotes { get; set; }

    /// <summary>
    /// Only relevant when Approve = true.
    /// If provided, links the resulting MemberExcuse to this member;
    /// otherwise the excuse is created using the submitted MemberName and MemberCustomId.
    /// </summary>
    public Guid? MemberId { get; set; }
}
