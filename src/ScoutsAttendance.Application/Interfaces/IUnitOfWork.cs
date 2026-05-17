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
    /// Executes <paramref name="action"/> inside a single database transaction.
    /// Commits on success; rolls back and re-throws on any exception.
    /// Use this to wrap raw-SQL calls and EF saves that must succeed or fail together.
    /// </summary>
    Task ExecuteInTransactionAsync(Func<Task> action);

    /// <summary>
    /// Directly NULL-outs TroopId for every non-deleted member belonging to
    /// <paramref name="troopId"/> using a raw SQL UPDATE.  Bypasses EF
    /// change-tracking so it works even when the EF model / DB schema are
    /// temporarily out of sync.
    /// </summary>
    Task<int> UnassignMembersFromTroopAsync(Guid troopId, DateTime updatedAt);

    /// <summary>
    /// Directly NULL-outs TroopId for every non-deleted user (e.g. troop
    /// leaders, attendance-only users) whose account is scoped to
    /// <paramref name="troopId"/>.  This ensures that stale JWT claims — which
    /// carry the TroopId at login time and are never re-issued mid-session —
    /// cannot restrict their member visibility after the troop is deleted.
    /// </summary>
    Task<int> UnassignUsersFromTroopAsync(Guid troopId, DateTime updatedAt);
}
