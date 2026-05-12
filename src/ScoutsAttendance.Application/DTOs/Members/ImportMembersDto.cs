namespace ScoutsAttendance.Application.DTOs.Members;

public class ImportMembersResultDto
{
    public int ImportedCount { get; set; }
    public int SkippedCount  { get; set; }
    public List<SkippedRowDto> SkippedRows { get; set; } = [];
}

public class SkippedRowDto
{
    public int    RowNumber { get; set; }
    public string? FirstName { get; set; }
    public string? LastName  { get; set; }
    public string  Reason   { get; set; } = string.Empty;
}
