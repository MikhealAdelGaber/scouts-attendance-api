using System.ComponentModel.DataAnnotations;

namespace ScoutsAttendance.Application.DTOs.Talaea;

public class TalaeaDto
{
    public Guid    Id          { get; set; }
    public string  Name        { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid    TroopId     { get; set; }
    public string  TroopName   { get; set; } = string.Empty;
    public Guid    GroupId     { get; set; }
    public int     MemberCount { get; set; }
    public decimal TotalPoints { get; set; }
    public DateTime CreatedAt  { get; set; }
}

public class CreateTalaeaDto
{
    [Required] public string   Name        { get; set; } = string.Empty;
    public string?   Description { get; set; }
    [Required] public Guid     TroopId     { get; set; }
}

public class UpdateTalaeaDto
{
    [Required] public string   Name        { get; set; } = string.Empty;
    public string?   Description { get; set; }
}

// ─── Talaea Points ─────────────────────────────────────────────────────────────

public class TalaeaPointsDto
{
    public Guid    Id           { get; set; }
    public Guid    TalaeaId     { get; set; }
    public string  TalaeaName   { get; set; } = string.Empty;
    public Guid    CategoryId   { get; set; }
    public string  CategoryName { get; set; } = string.Empty;
    public decimal Points       { get; set; }
    public DateTime Date        { get; set; }
    public string? Note         { get; set; }
}

public class AddTalaeaPointsDto
{
    [Required] public Guid     TalaeaId   { get; set; }
    [Required] public Guid     CategoryId { get; set; }
    [Required, Range(0.1, 10000)] public decimal Points { get; set; }
    public string?   Note      { get; set; }
    public DateTime? Date      { get; set; }
}

public class TalaeaPointsSummaryDto
{
    public Guid    TalaeaId    { get; set; }
    public string  TalaeaName  { get; set; } = string.Empty;
    public string  TroopName   { get; set; } = string.Empty;
    public decimal TotalPoints { get; set; }
    public List<TalaeaPointsDto>        History    { get; set; } = [];
    public Dictionary<string, decimal>  ByCategory { get; set; } = [];
}
