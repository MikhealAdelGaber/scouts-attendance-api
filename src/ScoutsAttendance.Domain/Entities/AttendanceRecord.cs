using ScoutsAttendance.Domain.Common;
using ScoutsAttendance.Domain.Enums;

namespace ScoutsAttendance.Domain.Entities;

public class AttendanceRecord : BaseEntity
{
    public Guid EventId { get; set; }
    public Guid MemberId { get; set; }
    public AttendanceStatus Status { get; set; }
    public string? Notes { get; set; }
    public DateTime MarkedAt { get; set; } = DateTime.UtcNow;
    public Guid MarkedBy { get; set; }

    public Event Event { get; set; } = null!;
    public Member Member { get; set; } = null!;
    public MemberPoints? AutoPoints { get; set; }
}
