namespace ScoutsAttendance.Application.DTOs.Points;

public class TroopRankingDto
{
    public int Rank { get; set; }
    public Guid TroopId { get; set; }
    public string TroopName { get; set; } = string.Empty;
    public string GroupName { get; set; } = string.Empty;
    public decimal TotalPoints { get; set; }
    public int MemberCount { get; set; }
    public decimal PointsChange { get; set; }
}

public class MemberRankingDto
{
    public int Rank { get; set; }
    public Guid MemberId { get; set; }
    public string MemberName { get; set; } = string.Empty;
    public string TroopName { get; set; } = string.Empty;
    public string GroupName { get; set; } = string.Empty;
    public decimal TotalPoints { get; set; }
    public decimal AttendancePoints { get; set; }
    public decimal BonusPoints { get; set; }
}

public class LeaderboardDto
{
    public List<TroopRankingDto> TroopRankings { get; set; } = [];
    public List<MemberRankingDto> TopMembers { get; set; } = [];
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}
