using System.ComponentModel.DataAnnotations;

namespace ScoutsAttendance.Application.DTOs.ExamScores;

public class ExamScoreDto
{
    public Guid    Id         { get; set; }
    public Guid    MemberId   { get; set; }
    public string  MemberName { get; set; } = string.Empty;
    public string  TroopName  { get; set; } = string.Empty;
    public int     Year       { get; set; }
    public decimal Score      { get; set; }
    public string? Notes      { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateExamScoreDto
{
    [Required] public Guid    MemberId { get; set; }
    [Required] public int     Year     { get; set; }
    [Required, Range(0, 100)] public decimal Score { get; set; }
    public string? Notes { get; set; }
}

public class UpdateExamScoreDto
{
    [Required, Range(0, 100)] public decimal Score { get; set; }
    public string? Notes { get; set; }
}
