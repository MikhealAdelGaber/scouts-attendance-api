using Microsoft.EntityFrameworkCore;
using ScoutsAttendance.Application.Interfaces;
using ScoutsAttendance.Domain.Entities;
using ScoutsAttendance.Infrastructure.Data;

namespace ScoutsAttendance.Infrastructure.Repositories;

public class UnitOfWork : IUnitOfWork
{
    private readonly ApplicationDbContext _context;

    public UnitOfWork(ApplicationDbContext context)
    {
        _context = context;
        Users             = new GenericRepository<User>(context);
        Groups            = new GenericRepository<Group>(context);
        Troops            = new GenericRepository<Troop>(context);
        Members           = new GenericRepository<Member>(context);
        Events            = new GenericRepository<Event>(context);
        AttendanceRecords = new GenericRepository<AttendanceRecord>(context);
        PointCategories       = new GenericRepository<PointCategory>(context);
        MemberPointCategories = new GenericRepository<MemberPointCategory>(context);
        TroopPointCategories  = new GenericRepository<TroopPointCategory>(context);
        MemberPoints          = new GenericRepository<MemberPoints>(context);
        TroopPoints           = new GenericRepository<TroopPoints>(context);
        Transfers         = new GenericRepository<Transfer>(context);
        MemberExcuses     = new GenericRepository<MemberExcuse>(context);
        MemberExamScores  = new GenericRepository<MemberExamScore>(context);
        PendingExcuses    = new GenericRepository<PendingExcuse>(context);

        // Trips
        Trips                 = new GenericRepository<Trip>(context);
        TripBookings          = new GenericRepository<TripBooking>(context);
        TripAttendanceRecords = new GenericRepository<TripAttendanceRecord>(context);
        BookingPayments       = new GenericRepository<BookingPayment>(context);

        // Badges
        Badges       = new GenericRepository<Badge>(context);
        MemberBadges = new GenericRepository<MemberBadge>(context);

        // Transfer Requests
        TransferRequests = new GenericRepository<MemberTransferRequest>(context);
        TransferArchives = new GenericRepository<MemberTransferArchive>(context);
    }

    public IRepository<User>             Users             { get; }
    public IRepository<Group>            Groups            { get; }
    public IRepository<Troop>            Troops            { get; }
    public IRepository<Member>           Members           { get; }
    public IRepository<Event>            Events            { get; }
    public IRepository<AttendanceRecord> AttendanceRecords { get; }
    public IRepository<PointCategory>       PointCategories       { get; }
    public IRepository<MemberPointCategory> MemberPointCategories { get; }
    public IRepository<TroopPointCategory>  TroopPointCategories  { get; }
    public IRepository<MemberPoints>        MemberPoints          { get; }
    public IRepository<TroopPoints>         TroopPoints           { get; }
    public IRepository<Transfer>         Transfers         { get; }
    public IRepository<MemberExcuse>     MemberExcuses     { get; }
    public IRepository<MemberExamScore>  MemberExamScores  { get; }
    public IRepository<PendingExcuse>    PendingExcuses    { get; }

    // Trips
    public IRepository<Trip>                 Trips                 { get; }
    public IRepository<TripBooking>          TripBookings          { get; }
    public IRepository<TripAttendanceRecord> TripAttendanceRecords { get; }
    public IRepository<BookingPayment>       BookingPayments       { get; }

    // Badges
    public IRepository<Badge>       Badges       { get; }
    public IRepository<MemberBadge> MemberBadges { get; }

    // Transfer Requests
    public IRepository<MemberTransferRequest>  TransferRequests  { get; }
    public IRepository<MemberTransferArchive>  TransferArchives  { get; }

    public async Task<int> SaveChangesAsync() => await _context.SaveChangesAsync();

    /// <inheritdoc />
    public async Task<int> UnassignMembersFromTroopAsync(Guid troopId, DateTime updatedAt)
    {
        var isPostgres = _context.Database.ProviderName?
            .Contains("Npgsql", StringComparison.OrdinalIgnoreCase) ?? false;

        // Use raw SQL so the UPDATE goes straight to the database — this works even
        // when EF's change-tracker has no knowledge of these member rows, and even
        // when the DB schema hasn't had the NOT NULL constraint dropped yet (the
        // DbSeeder ALTER TABLE runs at startup, but a raw UPDATE to NULL still needs
        // the column to be nullable — which the DbSeeder ensures).
        if (isPostgres)
        {
            return await _context.Database.ExecuteSqlRawAsync(
                @"UPDATE ""Members"" SET ""TroopId"" = NULL, ""UpdatedAt"" = {0}
                  WHERE ""TroopId"" = {1} AND ""IsDeleted"" = FALSE",
                updatedAt, troopId);
        }
        else
        {
            return await _context.Database.ExecuteSqlRawAsync(
                @"UPDATE [Members] SET [TroopId] = NULL, [UpdatedAt] = {0}
                  WHERE [TroopId] = {1} AND [IsDeleted] = 0",
                updatedAt, troopId);
        }
    }

    /// <inheritdoc />
    public async Task<int> UnassignUsersFromTroopAsync(Guid troopId, DateTime updatedAt)
    {
        var isPostgres = _context.Database.ProviderName?
            .Contains("Npgsql", StringComparison.OrdinalIgnoreCase) ?? false;

        if (isPostgres)
        {
            return await _context.Database.ExecuteSqlRawAsync(
                @"UPDATE ""Users"" SET ""TroopId"" = NULL, ""UpdatedAt"" = {0}
                  WHERE ""TroopId"" = {1} AND ""IsDeleted"" = FALSE",
                updatedAt, troopId);
        }
        else
        {
            return await _context.Database.ExecuteSqlRawAsync(
                @"UPDATE [Users] SET [TroopId] = NULL, [UpdatedAt] = {0}
                  WHERE [TroopId] = {1} AND [IsDeleted] = 0",
                updatedAt, troopId);
        }
    }

    /// <inheritdoc />
    public async Task ExecuteInTransactionAsync(Func<Task> action)
    {
        await using var tx = await _context.Database.BeginTransactionAsync();
        try
        {
            await action();
            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<int> DeleteMemberPointsAsync(Guid memberId)
    {
        var isPostgres = _context.Database.ProviderName?
            .Contains("Npgsql", StringComparison.OrdinalIgnoreCase) ?? false;

        if (isPostgres)
        {
            return await _context.Database.ExecuteSqlRawAsync(
                @"DELETE FROM ""MemberPoints"" WHERE ""MemberId"" = {0}",
                memberId);
        }
        else
        {
            return await _context.Database.ExecuteSqlRawAsync(
                @"DELETE FROM [MemberPoints] WHERE [MemberId] = {0}",
                memberId);
        }
    }

    /// <inheritdoc />
    public async Task<int> DeleteMemberAttendanceRecordsAsync(Guid memberId)
    {
        var isPostgres = _context.Database.ProviderName?
            .Contains("Npgsql", StringComparison.OrdinalIgnoreCase) ?? false;

        if (isPostgres)
        {
            return await _context.Database.ExecuteSqlRawAsync(
                @"DELETE FROM ""AttendanceRecords"" WHERE ""MemberId"" = {0}",
                memberId);
        }
        else
        {
            return await _context.Database.ExecuteSqlRawAsync(
                @"DELETE FROM [AttendanceRecords] WHERE [MemberId] = {0}",
                memberId);
        }
    }

    public void Dispose() => _context.Dispose();
}
