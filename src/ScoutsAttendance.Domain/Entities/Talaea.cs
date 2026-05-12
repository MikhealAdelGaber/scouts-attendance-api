using ScoutsAttendance.Domain.Common;

namespace ScoutsAttendance.Domain.Entities;

/// <summary>Talaea – a scout sub-group within a troop (typically 5-8 scouts).</summary>
public class Talaea : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid TroopId { get; set; }
    public Guid GroupId { get; set; }

    public Troop  Troop  { get; set; } = null!;
    public Group  Group  { get; set; } = null!;

    public ICollection<Member>       Members      { get; set; } = new List<Member>();
    public ICollection<TalaeaPoints> TalaeaPoints { get; set; } = new List<TalaeaPoints>();
}
