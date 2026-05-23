using System.ComponentModel.DataAnnotations;
using ScoutsAttendance.Domain.Enums;

namespace ScoutsAttendance.Application.DTOs.PendingExcuses;

// ── Member info exposed publicly (no PII beyond name + scout ID) ─────────────
public class PublicMemberDto
{
    public Guid   Id       { get; set; }
    public string FullName { get; set; } = string.Empty;
    public int    CustomId { get; set; }
}

// ── Public troop info returned to anonymous submitters ───────────────────────
public class PublicTroopInfoDto
{
    public Guid   Id        { get; set; }
    public string Name      { get; set; } = string.Empty;
    public string GroupName { get; set; } = string.Empty;

    /// <summary>All active members of this troop, for the searchable dropdown.</summary>
    public IEnumerable<PublicMemberDto> Members { get; set; } = Enumerable.Empty<PublicMemberDto>();
}

// ── Submission payload sent by an anonymous user ─────────────────────────────
public class SubmitPendingExcuseDto
{
    /// <summary>Name of the person submitting (parent / guardian / scout).</summary>
    [Required, MaxLength(200)]
    public string SubmittedByName { get; set; } = string.Empty;

    /// <summary>The member this excuse is for — must belong to this troop.</summary>
    [Required]
    public Guid MemberId { get; set; }

    [Required]
    public DateTime StartDate { get; set; }

    [Required]
    public DateTime EndDate { get; set; }

    [Required, MaxLength(1000)]
    public string Reason { get; set; } = string.Empty;
}

// ── Full DTO returned to admins / leaders ────────────────────────────────────
public class PendingExcuseDto
{
    public Guid               Id               { get; set; }
    public Guid               TroopId          { get; set; }
    public string             TroopName        { get; set; } = string.Empty;
    public Guid               MemberId         { get; set; }
    public string             MemberName       { get; set; } = string.Empty;
    public int                MemberCustomId   { get; set; }
    public string             SubmittedByName  { get; set; } = string.Empty;
    public DateTime           StartDate        { get; set; }
    public DateTime           EndDate          { get; set; }
    public string             Reason           { get; set; } = string.Empty;
    public PendingExcuseStatus Status          { get; set; }
    public string             StatusName       { get; set; } = string.Empty;
    public string?            ReviewNotes      { get; set; }
    public DateTime?          ReviewedAt       { get; set; }
    public Guid?              ResultingExcuseId { get; set; }
    public DateTime           CreatedAt        { get; set; }
}

// ── Review notes payload (used by approve and reject endpoints) ───────────────
public class ReviewNotesDto
{
    [MaxLength(500)]
    public string? ReviewNotes { get; set; }
}
