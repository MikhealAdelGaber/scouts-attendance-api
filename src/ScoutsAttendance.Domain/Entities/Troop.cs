using ScoutsAttendance.Domain.Common;

namespace ScoutsAttendance.Domain.Entities;

public class Troop : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public Guid GroupId { get; set; }
    public Guid? LeaderId { get; set; }

    public Group Group { get; set; } = null!;
    public User? Leader { get; set; }
    public ICollection<Member> Members { get; set; } = new List<Member>();
    public ICollection<TroopPoints> TroopPoints { get; set; } = new List<TroopPoints>();
}
