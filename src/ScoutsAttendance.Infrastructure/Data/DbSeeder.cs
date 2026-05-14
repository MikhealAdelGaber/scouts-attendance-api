using Microsoft.EntityFrameworkCore;
using ScoutsAttendance.Domain.Entities;
using ScoutsAttendance.Domain.Enums;

namespace ScoutsAttendance.Infrastructure.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(ApplicationDbContext context)
    {
        // PostgreSQL (Railway): EF Core migrations contain SQL Server-specific type names
        // (uniqueidentifier, nvarchar) that PostgreSQL rejects. Use EnsureCreated() on a
        // fresh deployment instead, which creates tables directly from the model.
        // SQL Server (local dev): use Migrate() to apply the full migration history.
        var isPostgres = context.Database.ProviderName?
            .Contains("Npgsql", StringComparison.OrdinalIgnoreCase) ?? false;

        if (isPostgres)
        {
            // Step 1: try to create the schema
            await context.Database.EnsureCreatedAsync();

            // Step 2: verify the Users table actually exists.
            // If a previous failed migration left only __EFMigrationsHistory behind,
            // EnsureCreated returns false without creating model tables. Wipe and redo.
            try
            {
                await context.Users.AnyAsync();
            }
            catch
            {
                await context.Database.EnsureDeletedAsync();
                await context.Database.EnsureCreatedAsync();
            }

            // Step 3: Ensure Members.TroopId is nullable (idempotent ALTER TABLE).
            // Databases created before this change have TroopId as NOT NULL.
            // Dropping the NOT NULL constraint lets us set TroopId = NULL when a
            // troop is deleted, so no member data is ever lost.
            try
            {
                await context.Database.ExecuteSqlRawAsync(@"
                    DO $$ BEGIN
                        IF EXISTS (
                            SELECT 1 FROM information_schema.columns
                            WHERE table_name  = 'Members'
                              AND column_name = 'TroopId'
                              AND is_nullable = 'NO'
                        ) THEN
                            ALTER TABLE ""Members"" ALTER COLUMN ""TroopId"" DROP NOT NULL;
                        END IF;
                    END $$;
                ");
            }
            catch
            {
                // Table not yet created, or already nullable — safe to ignore.
            }
        }
        else
        {
            await context.Database.MigrateAsync();
        }

        if (await context.Users.AnyAsync()) return;

        var adminId = Guid.NewGuid();
        var admin = new User
        {
            Id = adminId,
            Username = "admin",
            Email = "admin@scouts.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@123"),
            Role = UserRole.SystemAdmin,
            IsActive = true,
            CanTakeAttendance = false,
            CanEditMembers    = true,
            CanCreateEvents   = true
        };

        await context.Users.AddAsync(admin);

        var groupLeaderId = Guid.NewGuid();
        var groupLeader = new User
        {
            Id = groupLeaderId,
            Username = "groupleader",
            Email = "groupleader@scouts.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Leader@123"),
            Role = UserRole.GroupLeader,
            IsActive = true,
            CanTakeAttendance = false,
            CanEditMembers    = true,
            CanCreateEvents   = true
        };
        await context.Users.AddAsync(groupLeader);

        var group = new Group
        {
            Name = "Eagle Scouts Group",
            Description = "Main scouts group",
            LeaderId = groupLeaderId
        };
        await context.Groups.AddAsync(group);
        await context.SaveChangesAsync();

        groupLeader.GroupId = group.Id;
        context.Users.Update(groupLeader);

        var troopLeaderId = Guid.NewGuid();
        var troopLeader = new User
        {
            Id = troopLeaderId,
            Username = "troopleader",
            Email = "troopleader@scouts.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Leader@123"),
            Role = UserRole.GroupLeader,   // TroopLeader role removed; seed as GroupLeader
            GroupId = group.Id,
            IsActive = true,
            CanTakeAttendance = true,
            CanEditMembers    = true,
            CanCreateEvents   = true
        };
        await context.Users.AddAsync(troopLeader);

        var troop = new Troop
        {
            Name = "Falcon Troop",
            GroupId = group.Id,
            LeaderId = troopLeaderId
        };
        await context.Troops.AddAsync(troop);
        await context.SaveChangesAsync();

        troopLeader.TroopId = troop.Id;
        context.Users.Update(troopLeader);

        // Demo AttendanceOnly user
        var attendanceUser = new User
        {
            Username = "attendance",
            Email = "attendance@scouts.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Attend@123"),
            Role = UserRole.AttendanceOnly,
            GroupId = group.Id,
            TroopId = troop.Id,
            IsActive = true,
            CanTakeAttendance = true,
            CanEditMembers    = false,
            CanCreateEvents   = false
        };
        await context.Users.AddAsync(attendanceUser);
        await context.SaveChangesAsync();

        // ── Member Point Categories ────────────────────────────────────────────────
        var memberCats = new[]
        {
            new MemberPointCategory { Name = "Attendance",   Description = "Auto-awarded for attending events",   GroupId = group.Id, AttendancePresentPoints = 1m, AttendanceLatePoints = 0.5m },
            new MemberPointCategory { Name = "Behavior",     Description = "Points for good behavior",            GroupId = group.Id },
            new MemberPointCategory { Name = "Activity",     Description = "Points for participation in activities", GroupId = group.Id },
            new MemberPointCategory { Name = "Exam",         Description = "Points for exam performance",         GroupId = group.Id },
            new MemberPointCategory { Name = "Discipline",   Description = "Points for discipline and conduct",   GroupId = group.Id },
        };
        await context.MemberPointCategories.AddRangeAsync(memberCats);

        // ── Troop Point Categories ─────────────────────────────────────────────────
        var troopCats = new[]
        {
            new TroopPointCategory { Name = "Competition",        Description = "Points for competition results",      GroupId = group.Id },
            new TroopPointCategory { Name = "Community Service",  Description = "Points for community service",        GroupId = group.Id },
            new TroopPointCategory { Name = "Event Performance",  Description = "Points for overall event performance", GroupId = group.Id },
            new TroopPointCategory { Name = "Scout Challenge",    Description = "Points for completing scout challenges", GroupId = group.Id },
            new TroopPointCategory { Name = "Bonus",              Description = "Bonus points for the troop",          GroupId = group.Id },
        };
        await context.TroopPointCategories.AddRangeAsync(troopCats);

        var members = new List<Member>();
        var seedNames = new[] {
            ("Ahmed",   "Hassan",  Domain.Enums.Gender.Male,   100001),
            ("Sara",    "Mohamed", Domain.Enums.Gender.Female, 100002),
            ("Omar",    "Ali",     Domain.Enums.Gender.Male,   100003),
            ("Nour",    "Ibrahim", Domain.Enums.Gender.Female, 100004),
            ("Youssef", "Khaled",  Domain.Enums.Gender.Male,   100005),
        };
        foreach (var (first, last, gender, customId) in seedNames)
        {
            var m = new Member
            {
                FirstName   = first,
                LastName    = last,
                Gender      = gender,
                CustomId    = customId,
                TroopId     = troop.Id,
                GroupId     = group.Id,
                DateOfBirth = new DateTime(2010, 1, 1)
            };
            m.QrCode = $"SCOUT-{customId}";
            members.Add(m);
        }
        await context.Members.AddRangeAsync(members);

        await context.SaveChangesAsync();
    }
}
