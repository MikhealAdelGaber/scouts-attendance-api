namespace ScoutsAttendance.Application.DTOs.Dashboard;

public class DashboardStatsDto
{
    public int    TotalMembers          { get; set; }
    public int    TotalTroops           { get; set; }
    public int    TotalEvents           { get; set; }
    public double OverallAttendanceRate { get; set; }

    public List<TroopAttendanceStatsDto> TroopBreakdown { get; set; } = [];
}

public class TroopAttendanceStatsDto
{
    public Guid   TroopId                { get; set; }
    public string TroopName              { get; set; } = string.Empty;
    public string GroupName              { get; set; } = string.Empty;
    public int    MemberCount            { get; set; }
    public int    TotalAttendanceRecords { get; set; }
    public int    PresentCount           { get; set; }
    public int    AbsentCount            { get; set; }
    public double AttendanceRate         { get; set; }
    public double AbsenceRate            { get; set; }
}
