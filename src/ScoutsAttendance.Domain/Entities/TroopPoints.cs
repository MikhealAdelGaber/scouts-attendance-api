using ScoutsAttendance.Domain.Common;

namespace ScoutsAttendance.Domain.Entities;

public class TroopPoints : BaseEntity
{
    public Guid     TroopId              { get; set; }
    public Guid?    TroopPointCategoryId { get; set; }   // replaces CategoryId → PointCategory
    public decimal  Points               { get; set; }
    public DateTime Date                 { get; set; } = DateTime.UtcNow;
    public string?  Note                 { get; set; }
    public Guid     AddedBy              { get; set; }

    public Troop               Troop    { get; set; } = null!;
    public TroopPointCategory? Category { get; set; }
}
