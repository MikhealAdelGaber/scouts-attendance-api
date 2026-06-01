using System.ComponentModel.DataAnnotations;
using ScoutsAttendance.Domain.Enums;

namespace ScoutsAttendance.Application.DTOs.Reports;

// ─── Template CRUD ────────────────────────────────────────────────────────────

public class ReportTemplateDto
{
    public Guid      Id          { get; set; }
    public string    Name        { get; set; } = string.Empty;
    public Guid      GroupId     { get; set; }
    public string    GroupName   { get; set; } = string.Empty;
    public Guid?     TroopId     { get; set; }
    public string?   TroopName   { get; set; }
    public string    CreatedBy   { get; set; } = string.Empty;
    public bool      IsActive    { get; set; }
    public decimal   TotalWeight { get; set; }
    public DateTime  CreatedAt   { get; set; }
    public List<ReportTemplateCategoryDto> Categories { get; set; } = new();
}

public class ReportTemplateCategoryDto
{
    public Guid         Id               { get; set; }
    public CategoryType CategoryType     { get; set; }
    public string       CategoryName     { get; set; } = string.Empty;
    public decimal      Weight           { get; set; }
    public string?      CustomDescription { get; set; }
    public int          SortOrder        { get; set; }
}

public class CreateReportTemplateDto
{
    [Required, MaxLength(200)] public string Name    { get; set; } = string.Empty;
    public Guid?                             TroopId { get; set; }
    [Required, MinLength(1)]   public List<CreateCategoryDto> Categories { get; set; } = new();
}

public class UpdateReportTemplateDto
{
    [Required, MaxLength(200)] public string Name     { get; set; } = string.Empty;
    public Guid?                             TroopId  { get; set; }
    public bool                              IsActive { get; set; } = true;
    [Required, MinLength(1)]   public List<CreateCategoryDto> Categories { get; set; } = new();
}

public class CreateCategoryDto
{
    [Required]                              public CategoryType CategoryType     { get; set; }
    [Required, MaxLength(200)]              public string       CategoryName     { get; set; } = string.Empty;
    [Required, Range(0.01, 100)]            public decimal      Weight           { get; set; }
    [MaxLength(500)]                        public string?      CustomDescription { get; set; }
    public int                              SortOrder        { get; set; }
}

// ─── Custom score entry ───────────────────────────────────────────────────────

public class SaveCustomScoresDto
{
    [Required] public Guid CategoryId { get; set; }
    [Required] public List<MemberCustomScoreItemDto> Scores { get; set; } = new();
}

public class MemberCustomScoreItemDto
{
    [Required]              public Guid    MemberId { get; set; }
    [Range(0, 100)]         public decimal Score    { get; set; }
    [MaxLength(500)]        public string? Notes    { get; set; }
}

// ─── Results ─────────────────────────────────────────────────────────────────

public class ReportResultsDto
{
    public ReportTemplateDto         Template         { get; set; } = new();
    public List<MemberReportResultDto> Results        { get; set; } = new();
    public decimal  PassThreshold    { get; set; } = 50;
    public int      PassCount        { get; set; }
    public int      FailCount        { get; set; }
    public decimal  ClassAverage     { get; set; }
    public List<CategoryAverageDto>  CategoryAverages { get; set; } = new();
}

public class MemberReportResultDto
{
    public int      Rank         { get; set; }
    public Guid     MemberId     { get; set; }
    public string   MemberName   { get; set; } = string.Empty;
    public int      CustomId     { get; set; }
    public string?  TroopName    { get; set; }
    public decimal  FinalScore   { get; set; }
    public string   Grade        { get; set; } = string.Empty;
    public string   GradeArabic  { get; set; } = string.Empty;
    public bool     IsPassing    { get; set; }
    public List<CategoryScoreItemDto> CategoryScores { get; set; } = new();
}

public class CategoryScoreItemDto
{
    public Guid    CategoryId       { get; set; }
    public decimal RawRate          { get; set; }   // 0-100 (e.g. 80 = 80% attendance)
    public decimal ContributedScore { get; set; }   // RawRate * Weight / 100
    public string  Tooltip          { get; set; } = string.Empty;
}

public class CategoryAverageDto
{
    public Guid    CategoryId   { get; set; }
    public string  CategoryName { get; set; } = string.Empty;
    public decimal AverageScore { get; set; }
    public decimal AverageRate  { get; set; }
}

// ─── Member profile summary ───────────────────────────────────────────────────

public class MemberReportSummaryDto
{
    public Guid     TemplateId   { get; set; }
    public string   TemplateName { get; set; } = string.Empty;
    public decimal  FinalScore   { get; set; }
    public string   Grade        { get; set; } = string.Empty;
    public string   GradeArabic  { get; set; } = string.Empty;
    public bool     IsPassing    { get; set; }
    public List<CategoryScoreItemDto>      CategoryScores { get; set; } = new();
    public List<ReportTemplateCategoryDto> Categories     { get; set; } = new();
}

// ─── Custom scores for a category (read) ─────────────────────────────────────

public class CategoryCustomScoresDto
{
    public Guid   CategoryId   { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public List<MemberCustomScoreReadDto> MemberScores { get; set; } = new();
}

public class MemberCustomScoreReadDto
{
    public Guid    MemberId   { get; set; }
    public string  MemberName { get; set; } = string.Empty;
    public int     CustomId   { get; set; }
    public string? TroopName  { get; set; }
    public decimal Score      { get; set; }
    public string? Notes      { get; set; }
    public string? EnteredBy  { get; set; }
}
