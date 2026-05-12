using ScoutsAttendance.Domain.Common;

namespace ScoutsAttendance.Domain.Entities;

public class TroopPointCategory : BaseEntity
{
    public string  Name        { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid?   GroupId     { get; set; }
    public bool    IsGlobal    { get; set; }

    public Group?                    Group       { get; set; }
    public ICollection<TroopPoints>  TroopPoints { get; set; } = [];
}
