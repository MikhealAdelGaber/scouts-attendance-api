namespace ScoutsAttendance.Domain.Enums;

public enum UserRole
{
    SystemAdmin       = 1,
    GroupLeader       = 2,
    // 3 was TroopLeader (removed) — value reserved, existing DB rows migrated to GroupLeader
    // 4 was Member (removed) — value reserved
    AttendanceOnly    = 5,  // Can only take attendance; no editing rights
    GroupLeaderAdmin  = 6   // GroupLeader + can manage users in own group
}
