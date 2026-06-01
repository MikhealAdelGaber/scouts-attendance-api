using BCrypt.Net;
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

        // Projects
        Projects      = new GenericRepository<Project>(context);
        ProjectScores = new GenericRepository<MemberProjectScore>(context);

        // Final Report
        ReportTemplates  = new GenericRepository<ReportTemplate>(context);
        ReportCategories = new GenericRepository<ReportTemplateCategory>(context);
        CustomScores     = new GenericRepository<MemberCustomScore>(context);

        // Transfer Requests
        TransferRequests = new GenericRepository<MemberTransferRequest>(context);
        TransferArchives = new GenericRepository<MemberTransferArchive>(context);

        // Yearly Archives
        YearlyArchives       = new GenericRepository<YearlyArchive>(context);
        YearlyMemberArchives = new GenericRepository<YearlyMemberArchive>(context);
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

    // Projects
    public IRepository<Project>             Projects      { get; }
    public IRepository<MemberProjectScore>  ProjectScores { get; }

    // Final Report
    public IRepository<ReportTemplate>         ReportTemplates  { get; }
    public IRepository<ReportTemplateCategory> ReportCategories { get; }
    public IRepository<MemberCustomScore>      CustomScores     { get; }

    // Transfer Requests
    public IRepository<MemberTransferRequest>  TransferRequests  { get; }
    public IRepository<MemberTransferArchive>  TransferArchives  { get; }

    // Yearly Archives
    public IRepository<YearlyArchive>       YearlyArchives       { get; }
    public IRepository<YearlyMemberArchive> YearlyMemberArchives { get; }

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

    /// <inheritdoc />
    public async Task<int> DeleteMemberExcusesAsync(Guid memberId)
    {
        var isPostgres = _context.Database.ProviderName?
            .Contains("Npgsql", StringComparison.OrdinalIgnoreCase) ?? false;

        if (isPostgres)
        {
            return await _context.Database.ExecuteSqlRawAsync(
                @"DELETE FROM ""MemberExcuses"" WHERE ""MemberId"" = {0}",
                memberId);
        }
        else
        {
            return await _context.Database.ExecuteSqlRawAsync(
                @"DELETE FROM [MemberExcuses] WHERE [MemberId] = {0}",
                memberId);
        }
    }

    /// <inheritdoc />
    public async Task<int> DeleteMemberPendingExcusesAsync(Guid memberId)
    {
        var isPostgres = _context.Database.ProviderName?
            .Contains("Npgsql", StringComparison.OrdinalIgnoreCase) ?? false;

        if (isPostgres)
        {
            return await _context.Database.ExecuteSqlRawAsync(
                @"DELETE FROM ""PendingExcuses"" WHERE ""MemberId"" = {0}",
                memberId);
        }
        else
        {
            return await _context.Database.ExecuteSqlRawAsync(
                @"DELETE FROM [PendingExcuses] WHERE [MemberId] = {0}",
                memberId);
        }
    }

    // ── Year-end global bulk deletes ──────────────────────────────────────────

    /// <inheritdoc />
    public async Task<int> DeleteAllMemberPointsGlobalAsync()
    {
        var isPostgres = _context.Database.ProviderName?
            .Contains("Npgsql", StringComparison.OrdinalIgnoreCase) ?? false;
        return isPostgres
            ? await _context.Database.ExecuteSqlRawAsync(@"DELETE FROM ""MemberPoints""")
            : await _context.Database.ExecuteSqlRawAsync(@"DELETE FROM [MemberPoints]");
    }

    /// <inheritdoc />
    public async Task<int> DeleteAllMemberExcusesGlobalAsync()
    {
        var isPostgres = _context.Database.ProviderName?
            .Contains("Npgsql", StringComparison.OrdinalIgnoreCase) ?? false;
        return isPostgres
            ? await _context.Database.ExecuteSqlRawAsync(@"DELETE FROM ""MemberExcuses""")
            : await _context.Database.ExecuteSqlRawAsync(@"DELETE FROM [MemberExcuses]");
    }

    /// <inheritdoc />
    public async Task<int> DeleteAllPendingExcusesGlobalAsync()
    {
        var isPostgres = _context.Database.ProviderName?
            .Contains("Npgsql", StringComparison.OrdinalIgnoreCase) ?? false;
        return isPostgres
            ? await _context.Database.ExecuteSqlRawAsync(@"DELETE FROM ""PendingExcuses""")
            : await _context.Database.ExecuteSqlRawAsync(@"DELETE FROM [PendingExcuses]");
    }

    /// <inheritdoc />
    public async Task<int> DeleteAllAttendanceRecordsGlobalAsync()
    {
        var isPostgres = _context.Database.ProviderName?
            .Contains("Npgsql", StringComparison.OrdinalIgnoreCase) ?? false;
        return isPostgres
            ? await _context.Database.ExecuteSqlRawAsync(@"DELETE FROM ""AttendanceRecords""")
            : await _context.Database.ExecuteSqlRawAsync(@"DELETE FROM [AttendanceRecords]");
    }

    /// <inheritdoc />
    public async Task<int> SoftDeleteAllEventsGlobalAsync()
    {
        var isPostgres = _context.Database.ProviderName?
            .Contains("Npgsql", StringComparison.OrdinalIgnoreCase) ?? false;
        var now = DateTime.UtcNow;
        return isPostgres
            ? await _context.Database.ExecuteSqlRawAsync(
                @"UPDATE ""Events"" SET ""IsDeleted"" = TRUE, ""UpdatedAt"" = {0} WHERE ""IsDeleted"" = FALSE", now)
            : await _context.Database.ExecuteSqlRawAsync(
                @"UPDATE [Events] SET [IsDeleted] = 1, [UpdatedAt] = {0} WHERE [IsDeleted] = 0", now);
    }

    /// <inheritdoc />
    public async Task<int> DeleteAllTripDataGlobalAsync()
    {
        var isPostgres = _context.Database.ProviderName?
            .Contains("Npgsql", StringComparison.OrdinalIgnoreCase) ?? false;
        var now = DateTime.UtcNow;
        int total = 0;

        if (isPostgres)
        {
            total += await _context.Database.ExecuteSqlRawAsync(@"DELETE FROM ""TripAttendanceRecords""");
            total += await _context.Database.ExecuteSqlRawAsync(@"DELETE FROM ""BookingPayments""");
            total += await _context.Database.ExecuteSqlRawAsync(@"DELETE FROM ""TripBookings""");
            total += await _context.Database.ExecuteSqlRawAsync(
                @"UPDATE ""Trips"" SET ""IsDeleted"" = TRUE, ""UpdatedAt"" = {0} WHERE ""IsDeleted"" = FALSE", now);
        }
        else
        {
            total += await _context.Database.ExecuteSqlRawAsync(@"DELETE FROM [TripAttendanceRecords]");
            total += await _context.Database.ExecuteSqlRawAsync(@"DELETE FROM [BookingPayments]");
            total += await _context.Database.ExecuteSqlRawAsync(@"DELETE FROM [TripBookings]");
            total += await _context.Database.ExecuteSqlRawAsync(
                @"UPDATE [Trips] SET [IsDeleted] = 1, [UpdatedAt] = {0} WHERE [IsDeleted] = 0", now);
        }
        return total;
    }

    /// <inheritdoc />
    public async Task<int> DeleteAllProjectDataGlobalAsync()
    {
        var isPostgres = _context.Database.ProviderName?
            .Contains("Npgsql", StringComparison.OrdinalIgnoreCase) ?? false;
        var now = DateTime.UtcNow;
        int total = 0;

        if (isPostgres)
        {
            total += await _context.Database.ExecuteSqlRawAsync(@"DELETE FROM ""MemberProjectScores""");
            total += await _context.Database.ExecuteSqlRawAsync(
                @"UPDATE ""Projects"" SET ""IsDeleted"" = TRUE, ""UpdatedAt"" = {0} WHERE ""IsDeleted"" = FALSE", now);
        }
        else
        {
            total += await _context.Database.ExecuteSqlRawAsync(@"DELETE FROM [MemberProjectScores]");
            total += await _context.Database.ExecuteSqlRawAsync(
                @"UPDATE [Projects] SET [IsDeleted] = 1, [UpdatedAt] = {0} WHERE [IsDeleted] = 0", now);
        }
        return total;
    }

    /// <inheritdoc />
    public async Task<int> DeleteAllTroopsGlobalAsync()
    {
        var isPostgres = _context.Database.ProviderName?
            .Contains("Npgsql", StringComparison.OrdinalIgnoreCase) ?? false;
        var now = DateTime.UtcNow;

        if (isPostgres)
        {
            // Unassign all members and users from their troops
            await _context.Database.ExecuteSqlRawAsync(
                @"UPDATE ""Members"" SET ""TroopId"" = NULL, ""UpdatedAt"" = {0} WHERE ""TroopId"" IS NOT NULL AND ""IsDeleted"" = FALSE", now);
            await _context.Database.ExecuteSqlRawAsync(
                @"UPDATE ""Users"" SET ""TroopId"" = NULL, ""UpdatedAt"" = {0} WHERE ""TroopId"" IS NOT NULL AND ""IsDeleted"" = FALSE", now);
            // Delete troop points first (FK → TroopPointCategories with SetNull, but let's be explicit)
            await _context.Database.ExecuteSqlRawAsync(@"DELETE FROM ""TroopPoints""");
            // Delete troop-scoped point categories
            await _context.Database.ExecuteSqlRawAsync(@"DELETE FROM ""TroopPointCategories""");
            // Soft-delete all troops
            return await _context.Database.ExecuteSqlRawAsync(
                @"UPDATE ""Troops"" SET ""IsDeleted"" = TRUE, ""UpdatedAt"" = {0} WHERE ""IsDeleted"" = FALSE", now);
        }
        else
        {
            await _context.Database.ExecuteSqlRawAsync(
                @"UPDATE [Members] SET [TroopId] = NULL, [UpdatedAt] = {0} WHERE [TroopId] IS NOT NULL AND [IsDeleted] = 0", now);
            await _context.Database.ExecuteSqlRawAsync(
                @"UPDATE [Users] SET [TroopId] = NULL, [UpdatedAt] = {0} WHERE [TroopId] IS NOT NULL AND [IsDeleted] = 0", now);
            await _context.Database.ExecuteSqlRawAsync(@"DELETE FROM [TroopPoints]");
            await _context.Database.ExecuteSqlRawAsync(@"DELETE FROM [TroopPointCategories]");
            return await _context.Database.ExecuteSqlRawAsync(
                @"UPDATE [Troops] SET [IsDeleted] = 1, [UpdatedAt] = {0} WHERE [IsDeleted] = 0", now);
        }
    }

    /// <inheritdoc />
    public async Task<bool> VerifyUserPasswordAsync(Guid userId, string plainPassword)
    {
        var user = await _context.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted);
        if (user is null) return false;
        return BCrypt.Net.BCrypt.Verify(plainPassword, user.PasswordHash);
    }

    public void Dispose() => _context.Dispose();
}
