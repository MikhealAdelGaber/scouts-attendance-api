namespace ScoutsAttendance.Domain.Enums;

public enum AttendanceStatus
{
    Present = 1,
    Late    = 2,
    Absent  = 3,
    Excused = 4,
    TooLate = 5   // arrived very late — points configurable per event (default 0)
}
