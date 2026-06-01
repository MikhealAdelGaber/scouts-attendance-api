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
    IRepository<PendingExcuse>    PendingExcuses    { get; }

    // ── Trips ─────────────────────────────────────────────────────────────────
    IRepository<Trip>                 Trips                 { get; }
    IRepository<TripBooking>          TripBookings          { get; }
    IRepository<TripAttendanceRecord> TripAttendanceRecords { get; }
    IRepository<BookingPayment>       BookingPayments       { get; }

    // ── Badges ────────────────────────────────────────────────────────────────
    IRepository<Badge>       Badges       { get; }
    IRepository<MemberBadge> MemberBadges { get; }

    // ── Projects ──────────────────────────────────────────────────────────────
    IRepository<Project>             Projects      { get; }
    IRepository<MemberProjectScore>  ProjectScores { get; }

    // ── Final Report ──────────────────────────────────────────────────────────
    IRepository<ReportTemplate>         ReportTemplates  { get; }
    IRepository<ReportTemplateCategory> ReportCategories { get; }
    IRepository<MemberCustomScore>      CustomScores     { get; }

    // ── Transfer Requests ─────────────────────────────────────────────────────
    IRepository<MemberTransferRequest>  TransferRequests  { get; }
    IRepository<MemberTransferArchive>  TransferArchives  { get; }

    // ── Yearly Archives ───────────────────────────────────────────────────────
    IRepository<YearlyArchive>       YearlyArchives       { get; }
    IRepository<YearlyMemberArchive> YearlyMemberArchives { get; }

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

    /// <summary>
    /// Hard-deletes ALL MemberPoints rows for <paramref name="memberId"/>
    /// using a single raw SQL DELETE.  Called during transfer approval to reset
    /// the member's score before they join their new group.
    /// MemberPoints MUST be deleted BEFORE AttendanceRecords because of the FK
    /// MemberPoints.AttendanceRecordId → AttendanceRecords.Id.
    /// </summary>
    Task<int> DeleteMemberPointsAsync(Guid memberId);

    /// <summary>
    /// Hard-deletes ALL AttendanceRecord rows for <paramref name="memberId"/>
    /// using a single raw SQL DELETE.  Called during transfer approval to give
    /// the member a clean attendance slate in their new group.
    /// Expects MemberPoints to have been deleted first (see <see cref="DeleteMemberPointsAsync"/>).
    /// </summary>
    Task<int> DeleteMemberAttendanceRecordsAsync(Guid memberId);

    /// <summary>
    /// Hard-deletes ALL MemberExcuse rows for <paramref name="memberId"/>.
    /// Called during transfer approval so the member starts with no active excuses
    /// in their new group.
    /// </summary>
    Task<int> DeleteMemberExcusesAsync(Guid memberId);

    /// <summary>
    /// Hard-deletes ALL PendingExcuse rows whose MemberId matches
    /// <paramref name="memberId"/>.  Cleans up any submitted-but-not-yet-reviewed
    /// excuse requests before the member joins their new group.
    /// </summary>
    Task<int> DeleteMemberPendingExcusesAsync(Guid memberId);

    // ── Year-end global bulk deletes ──────────────────────────────────────────

    /// <summary>Hard-deletes ALL MemberPoints rows across every member/group (year-end reset).</summary>
    Task<int> DeleteAllMemberPointsGlobalAsync();

    /// <summary>Hard-deletes ALL MemberExcuse rows across every member/group (year-end reset).</summary>
    Task<int> DeleteAllMemberExcusesGlobalAsync();

    /// <summary>Hard-deletes ALL PendingExcuse rows across every member/group (year-end reset).</summary>
    Task<int> DeleteAllPendingExcusesGlobalAsync();

    /// <summary>Hard-deletes ALL AttendanceRecord rows (year-end reset). Call AFTER MemberPoints.</summary>
    Task<int> DeleteAllAttendanceRecordsGlobalAsync();

    /// <summary>Soft-deletes ALL Events (year-end reset). Call AFTER AttendanceRecords.</summary>
    Task<int> SoftDeleteAllEventsGlobalAsync();

    /// <summary>Hard-deletes all Trip-related rows (TripAttendanceRecords, BookingPayments, TripBookings) then soft-deletes Trips.</summary>
    Task<int> DeleteAllTripDataGlobalAsync();

    /// <summary>Hard-deletes ALL MemberProjectScore rows, then soft-deletes all Projects.</summary>
    Task<int> DeleteAllProjectDataGlobalAsync();

    /// <summary>
    /// Unassigns all members + users from every troop (TroopId → NULL),
    /// deletes TroopPointCategories, then soft-deletes all Troops.
    /// Returns the number of troops soft-deleted.
    /// </summary>
    Task<int> DeleteAllTroopsGlobalAsync();

    /// <summary>
    /// Verifies <paramref name="plainPassword"/> against the BCrypt hash stored for
    /// <paramref name="userId"/>.  Returns false if user not found or hash mismatch.
    /// Used by the new-year reset to confirm the admin's identity server-side.
    /// </summary>
    Task<bool> VerifyUserPasswordAsync(Guid userId, string plainPassword);
}
