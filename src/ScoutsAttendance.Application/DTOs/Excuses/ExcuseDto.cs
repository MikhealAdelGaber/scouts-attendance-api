using System.ComponentModel.DataAnnotations;

namespace ScoutsAttendance.Application.DTOs.Excuses;

public class MemberExcuseDto
{
    public Guid      Id               { get; set; }
    public Guid      MemberId         { get; set; }
    public string    MemberName       { get; set; } = string.Empty;
    public DateTime  StartDate        { get; set; }
    public DateTime? EndDate          { get; set; }
    public string    Reason           { get; set; } = string.Empty;
    public bool      IsActive         { get; set; }
    public DateTime  CreatedAt        { get; set; }
    public bool      IsPermanent      => EndDate == null;
    public string    CreatedByUsername { get; set; } = string.Empty;
}

public class GrantExcuseDto
{
    [Required] public Guid     MemberId  { get; set; }
    [Required] public DateTime StartDate { get; set; }
    public DateTime? EndDate    { get; set; }   // null = permanent / open-ended
    [Required, MinLength(3)] public string Reason { get; set; } = string.Empty;
}

public class UpdateExcuseDto
{
    public DateTime? EndDate  { get; set; }
    public bool      IsActive { get; set; }
    [Required, MinLength(3)] public string Reason { get; set; } = string.Empty;
}
