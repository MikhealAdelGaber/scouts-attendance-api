using ScoutsAttendance.Domain.Common;

namespace ScoutsAttendance.Domain.Entities;

/// <summary>A badge in the global catalog (not yet awarded to anyone).</summary>
public class Badge : BaseEntity
{
    public string  Name        { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Category    { get; set; }   // e.g. "Skills", "Sports", "Community"

    public ICollection<MemberBadge> MemberBadges { get; set; } = new List<MemberBadge>();
}
