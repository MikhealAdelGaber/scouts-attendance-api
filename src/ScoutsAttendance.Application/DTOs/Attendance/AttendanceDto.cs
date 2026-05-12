using System.ComponentModel.DataAnnotations;
using ScoutsAttendance.Domain.Enums;

namespace ScoutsAttendance.Application.DTOs.Attendance;

public class AttendanceDto
{
    public Guid Id { get; set; }
    public Guid EventId { get; set; }
    public string EventName { get; set; } = string.Empty;
    public Guid MemberId { get; set; }
    public string MemberName { get; set; } = string.Empty;
    public string TroopName { get; set; } = string.Empty;
    public AttendanceStatus Status { get; set; }
    public string StatusName { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public DateTime MarkedAt { get; set; }
    public decimal? PointsAwarded { get; set; }
}

public class MarkAttendanceDto
{
    [Required] public Guid EventId { get; set; }
    [Required] public Guid MemberId { get; set; }
    [Required] public AttendanceStatus Status { get; set; }
    public string? Notes { get; set; }
}

public class BulkAttendanceDto
{
    [Required] public Guid EventId { get; set; }
    [Required] public List<MemberAttendanceItem> Records { get; set; } = [];
}

public class MemberAttendanceItem
{
    public Guid MemberId { get; set; }
    public AttendanceStatus Status { get; set; }
    public string? Notes { get; set; }
}

public class QrAttendanceDto
{
    [Required] public Guid EventId { get; set; }
    [Required] public string QrToken { get; set; } = string.Empty;
}

public class AttendanceSummaryDto
{
    public Guid EventId { get; set; }
    public string EventName { get; set; } = string.Empty;
    public int TotalMembers { get; set; }
    public int Present { get; set; }
    public int Late { get; set; }
    public int Absent { get; set; }
    public int Excused { get; set; }
    public double AttendanceRate { get; set; }
}
