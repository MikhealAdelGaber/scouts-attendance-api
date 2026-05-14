using ScoutsAttendance.Domain.Entities;

namespace ScoutsAttendance.Application.Interfaces;

public interface IUnitOfWork : IDisposable
{
    IRepository<User>             Users             { get; }
    IRepository<Group>            Groups            { get; }
    IRepository<Troop>            Troops            { get; }
    IRepository<Member>           Members           { get; }
    IRepository<Event>            Events            { get; }
    IRepository<AttendanceRecord> AttendanceRecords { get; }
    IRepository<PointCategory>       PointCategories       { get; }
    IRepository<MemberPointCategory> MemberPointCategories { get; }
    IRepository<TroopPointCategory>  TroopPointCategories  { get; }
    IRepository<MemberPoints>        MemberPoints          { get; }
    IRepository<TroopPoints>         TroopPoints           { get; }
    IRepository<Transfer>         Transfers         { get; }
    IRepository<MemberExcuse>     MemberExcuses     { get; }
    IRepository<MemberExamScore>  MemberExamScores  { get; }

    Task<int> SaveChangesAsync();

    /// <summary>
    /// Directly NULL-outs TroopId for every non-deleted member belonging to
    /// <paramref name="troopId"/> using a raw SQL UPDATE.  This bypasses EF
    /// change-tracking so it works even if the EF model / DB schema are
    /// temporarily out of sync.
    /// </summary>
    Task<int> UnassignMembersFromTroopAsync(Guid troopId, DateTime updatedAt);
}
