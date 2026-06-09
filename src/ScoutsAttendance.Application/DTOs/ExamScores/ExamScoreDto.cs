using System.ComponentModel.DataAnnotations;

namespace ScoutsAttendance.Application.DTOs.ExamScores;

public class ExamScoreDto
{
    public Guid     Id                 { get; set; }
    public Guid     MemberId           { get; set; }
    public string   MemberName         { get; set; } = string.Empty;
    public string   TroopName          { get; set; } = string.Empty;
    public int      Year               { get; set; }
    public decimal  TheoreticalScore   { get; set; }
    public decimal  PracticalScore     { get; set; }
    public decimal  TotalScore         { get; set; }  // = Theoretical + Practical
    public decimal? Percentage         { get; set; }  // TotalScore / TotalMax * 100, null if no config
    public string?  Grade              { get; set; }
    public string?  Notes              { get; set; }
    public DateTime CreatedAt          { get; set; }
}

public class CreateExamScoreDto
{
    [Required] public Guid    MemberId         { get; set; }
    [Required] public int     Year             { get; set; }
    [Required, Range(0, 9999)] public decimal  TheoreticalScore { get; set; }
    [Required, Range(0, 9999)] public decimal  PracticalScore   { get; set; }
    public string? Notes { get; set; }
}

public class UpdateExamScoreDto
{
    [Required, Range(0, 9999)] public decimal  TheoreticalScore { get; set; }
    [Required, Range(0, 9999)] public decimal  PracticalScore   { get; set; }
    public string? Notes { get; set; }
}

// ── Exam Score Config ──────────────────────────────────────────────────────────

public class ExamScoreConfigDto
{
    public Guid    Id                  { get; set; }
    public Guid    GroupId             { get; set; }
    public int     Year               { get; set; }
    public decimal TheoreticalMaxScore { get; set; }
    public decimal PracticalMaxScore   { get; set; }
    public decimal TotalMaxScore       => TheoreticalMaxScore + PracticalMaxScore;
}

public class SaveExamScoreConfigDto
{
    [Required] public int     Year               { get; set; }
    [Required, Range(1, 9999)] public decimal TheoreticalMaxScore { get; set; } = 50m;
    [Required, Range(1, 9999)] public decimal PracticalMaxScore   { get; set; } = 50m;
}

// ── Import / Export ────────────────────────────────────────────────────────────

public class ImportExamScoreResultDto
{
    public int                      ImportedCount { get; set; }
    public int                      SkippedCount  { get; set; }
    public List<ImportSkippedRowDto> SkippedRows  { get; set; } = [];
}

public class ImportSkippedRowDto
{
    public int    RowNumber  { get; set; }
    public string MemberId   { get; set; } = string.Empty;
    public string MemberName { get; set; } = string.Empty;
    public string Reason     { get; set; } = string.Empty;
}
