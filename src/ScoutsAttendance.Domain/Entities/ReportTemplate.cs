using ScoutsAttendance.Domain.Common;

namespace ScoutsAttendance.Domain.Entities;

public class ReportTemplate : BaseEntity
{
    public string  Name      { get; set; } = string.Empty;
    public Guid    GroupId   { get; set; }
    public Guid?   TroopId   { get; set; }
    public string  CreatedBy { get; set; } = string.Empty;
    public bool    IsActive  { get; set; } = true;

    // ── Navigation ────────────────────────────────────────────────────────────
    public Group  Group { get; set; } = null!;
    public Troop? Troop { get; set; }
    public ICollection<ReportTemplateCategory> Categories { get; set; } = new List<ReportTemplateCategory>();
}
