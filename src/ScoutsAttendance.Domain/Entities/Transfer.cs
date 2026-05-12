using ScoutsAttendance.Domain.Common;

namespace ScoutsAttendance.Domain.Entities;

public class Transfer : BaseEntity
{
    public Guid MemberId { get; set; }
    public Guid FromTroopId { get; set; }
    public Guid ToTroopId { get; set; }
    public DateTime TransferDate { get; set; }
    public string? Reason { get; set; }
    public Guid ApprovedBy { get; set; }

    public Member Member { get; set; } = null!;
    public Troop FromTroop { get; set; } = null!;
    public Troop ToTroop { get; set; } = null!;
}
