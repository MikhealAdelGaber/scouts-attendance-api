using System.ComponentModel.DataAnnotations;

namespace ScoutsAttendance.Application.DTOs.Badges;

// ─── Catalog DTOs ─────────────────────────────────────────────────────────────

public class BadgeDto
{
    public Guid    Id          { get; set; }
    public string  Name        { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Category    { get; set; }
    public DateTime CreatedAt  { get; set; }
    public int     AwardCount  { get; set; }   // how many members have this badge
}

public class CreateBadgeDto
{
    [Required, MaxLength(100)] public string  Name        { get; set; } = string.Empty;
    [MaxLength(500)]           public string? Description { get; set; }
    [MaxLength(50)]            public string? Category    { get; set; }
}

public class UpdateBadgeDto
{
    [Required, MaxLength(100)] public string  Name        { get; set; } = string.Empty;
    [MaxLength(500)]           public string? Description { get; set; }
    [MaxLength(50)]            public string? Category    { get; set; }
}

// ─── Member-Badge DTOs ────────────────────────────────────────────────────────

public class MemberBadgeDto
{
    public Guid      Id          { get; set; }
    public Guid      MemberId    { get; set; }
    public string    MemberName  { get; set; } = string.Empty;
    public Guid      BadgeId     { get; set; }
    public string    BadgeName   { get; set; } = string.Empty;
    public string?   BadgeCategory { get; set; }
    public DateTime  AwardedDate { get; set; }
    public Guid?     TroopId     { get; set; }
    public string?   TroopName   { get; set; }
    public string?   GroupName   { get; set; }
    public string    AwardedBy   { get; set; } = string.Empty;
    public string?   Notes       { get; set; }
}

public class AwardBadgeDto
{
    [Required] public Guid     BadgeId     { get; set; }
    [Required] public DateTime AwardedDate { get; set; }
    public string? Notes { get; set; }
}
