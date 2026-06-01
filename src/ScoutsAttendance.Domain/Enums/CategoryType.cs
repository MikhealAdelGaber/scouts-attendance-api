namespace ScoutsAttendance.Domain.Enums;

public enum CategoryType
{
    Attendance = 1,   // pulled from AttendanceRecords
    Points     = 2,   // pulled from MemberPoints
    ExamScore  = 3,   // pulled from MemberExamScores
    Project    = 4,   // pulled from MemberProjectScores
    Badges     = 5,   // count of MemberBadges
    Custom     = 6    // manually entered by GroupLeader
}
