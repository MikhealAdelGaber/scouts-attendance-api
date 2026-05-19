using System.ComponentModel.DataAnnotations;

namespace ScoutsAttendance.Application.DTOs.Events;

public class EventDto
{
    public Guid     Id              { get; set; }
    public string   Name            { get; set; } = string.Empty;
    public string?  Description     { get; set; }
    public DateTime EventDate       { get; set; }
    public Guid     GroupId         { get; set; }
    public string   GroupName       { get; set; } = string.Empty;
    public Guid?    TroopId         { get; set; }
    public string?  TroopName       { get; set; }
    public bool     IsActive        { get; set; }
    public decimal  PresentPoints   { get; set; }
    public decimal  LatePoints      { get; set; }
    public decimal  ExcusedPoints   { get; set; }
    public decimal  AbsentPoints    { get; set; }
    public int      AttendanceCount { get; set; }
    public DateTime CreatedAt       { get; set; }
}

public class CreateEventDto
{
    [Required] public string   Name          { get; set; } = string.Empty;
    public string?   Description             { get; set; }
    [Required] public DateTime EventDate     { get; set; }
    public Guid?     TroopId                 { get; set; }
    public Guid?     GroupId                 { get; set; }  // Required for SystemAdmin

    [Range(-10000, 10000)] public decimal PresentPoints  { get; set; } = 100m;
    [Range(-10000, 10000)] public decimal LatePoints     { get; set; } = 50m;
    [Range(-10000, 10000)] public decimal ExcusedPoints  { get; set; } = 50m;
    [Range(-10000, 10000)] public decimal AbsentPoints   { get; set; } = -10m;
}

public class UpdateEventDto
{
    [Required] public string   Name          { get; set; } = string.Empty;
    public string?   Description             { get; set; }
    [Required] public DateTime EventDate     { get; set; }
    public bool      IsActive                { get; set; }

    [Range(-10000, 10000)] public decimal PresentPoints  { get; set; } = 100m;
    [Range(-10000, 10000)] public decimal LatePoints     { get; set; } = 50m;
    [Range(-10000, 10000)] public decimal ExcusedPoints  { get; set; } = 50m;
    [Range(-10000, 10000)] public decimal AbsentPoints   { get; set; } = -10m;
}
