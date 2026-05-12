using ScoutsAttendance.Domain.Common;

namespace ScoutsAttendance.Domain.Entities;

public class PointCategory : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? GroupId { get; set; }
    public bool IsGlobal { get; set; } = false;
    public decimal AttendancePresentPoints { get; set; } = 1m;
    public decimal AttendanceLatePoints { get; set; } = 0.5m;

    public Group? Group { get; set; }
    // MemberPoints / TroopPoints no longer FK to this table — use MemberPointCategory / TroopPointCategory
}
