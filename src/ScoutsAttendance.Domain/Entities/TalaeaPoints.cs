using ScoutsAttendance.Domain.Common;

namespace ScoutsAttendance.Domain.Entities;

/// <summary>Points awarded to a Talaea (completely separate from individual member points).</summary>
public class TalaeaPoints : BaseEntity
{
    public Guid     TalaeaId   { get; set; }
    public Guid     CategoryId { get; set; }
    public decimal  Points     { get; set; }
    public DateTime Date       { get; set; } = DateTime.UtcNow;
    public string?  Note       { get; set; }
    public Guid     AddedBy    { get; set; }

    public Talaea        Talaea   { get; set; } = null!;
    public PointCategory Category { get; set; } = null!;
}
