using ScoutsAttendance.Domain.Common;

namespace ScoutsAttendance.Domain.Entities;

public class Group : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid LeaderId { get; set; }

    public User Leader { get; set; } = null!;
    public ICollection<Troop>               Troops               { get; set; } = new List<Troop>();
    public ICollection<PointCategory>       PointCategories      { get; set; } = new List<PointCategory>();
    public ICollection<MemberPointCategory> MemberPointCategories { get; set; } = [];
    public ICollection<TroopPointCategory>  TroopPointCategories  { get; set; } = [];
    public ICollection<Event>               Events               { get; set; } = new List<Event>();
    public ICollection<Member>              Members              { get; set; } = new List<Member>();
}
