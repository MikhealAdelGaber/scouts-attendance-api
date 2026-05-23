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
            // EnsureCreated returns false without creating model tables.
            //
            // IMPORTANT: Only wipe + recreate when the table is genuinely missing
            // (PostgreSQL error code 42P01 = undefined_table).  Do NOT wipe on any
            // other exception (e.g., transient connection errors) — that would
            // destroy all production data every time Railway restarts under load.
            try
            {
                await context.Users.AnyAsync();
            }
            catch (Exception ex) when (IsUndefinedTableError(ex))
            {
                // The Users table does not exist at all — create missing tables only.
                // NEVER call EnsureDeletedAsync here: that would wipe all production data.
                // EnsureCreated is safe: it creates tables that are missing and leaves
                // existing tables (with their data) completely untouched.
                await context.Database.EnsureCreatedAsync();
            }
            catch
            {
                // Any other error (connection issue, timeout, etc.) — do NOT wipe.
                // Let the app continue; the tables may be fine.
            }

            // ── Step 3: PostgreSQL schema fixes ────────────────────────────────
            // All ALTER statements are idempotent: ADD COLUMN IF NOT EXISTS / DROP NOT NULL
            // never errors on re-deployments or fresh DBs.  They run on EVERY startup so
            // the schema converges to the correct state regardless of when Railway first
            // created the database (EnsureCreated only creates tables once; subsequent
            // columns added via SQL Server migrations must be added here for PostgreSQL).

            // ── 3a. TroopId: make nullable (required for unassigning members when a
            //        troop is deleted).  No-op if already nullable.
            try
            {
                await context.Database.ExecuteSqlRawAsync(
                    @"ALTER TABLE ""Members"" ALTER COLUMN ""TroopId"" DROP NOT NULL");
            }
            catch { /* swallow — column may not exist yet on a brand-new deployment */ }

            // ── 3b/3c. FK constraint with ON DELETE SET NULL (idempotent via IF EXISTS)
            try
            {
                await context.Database.ExecuteSqlRawAsync(
                    @"ALTER TABLE ""Members"" DROP CONSTRAINT IF EXISTS ""FK_Members_Troops_TroopId""");
            }
            catch { /* safe to ignore */ }
            try
            {
                await context.Database.ExecuteSqlRawAsync(@"
                    ALTER TABLE ""Members""
                        ADD CONSTRAINT ""FK_Members_Troops_TroopId""
                        FOREIGN KEY (""TroopId"")
                        REFERENCES ""Troops""(""Id"")
                        ON DELETE SET NULL");
            }
            catch { /* safe to ignore — constraint may already exist with correct behaviour */ }

            // ── 3d–3n. ADD COLUMN IF NOT EXISTS for every column added after InitialCreate.
            //    Covers the case where Railway's DB was created at an earlier model snapshot
            //    and subsequent migrations (which run on SQL Server via Migrate()) were never
            //    applied to the PostgreSQL instance.
            //
            //    Order matters for NOT NULL columns: add the column with a DEFAULT so existing
            //    rows get a value, then we can leave the DEFAULT in place (PostgreSQL allows it).

            // From AddMajorFeatures migration
            foreach (var sql in new[]
            {
                @"ALTER TABLE ""Members"" ADD COLUMN IF NOT EXISTS ""AcademicYear"" TEXT",
                @"ALTER TABLE ""Members"" ADD COLUMN IF NOT EXISTS ""Address""     TEXT",
                @"ALTER TABLE ""Members"" ADD COLUMN IF NOT EXISTS ""FatherPhone"" TEXT",
                @"ALTER TABLE ""Members"" ADD COLUMN IF NOT EXISTS ""MotherPhone"" TEXT",
                @"ALTER TABLE ""Members"" ADD COLUMN IF NOT EXISTS ""YearJoined""  INTEGER",
                @"ALTER TABLE ""Members"" ADD COLUMN IF NOT EXISTS ""HasNeckerchief"" BOOLEAN NOT NULL DEFAULT false",
            })
            {
                try { await context.Database.ExecuteSqlRawAsync(sql); } catch { /* safe */ }
            }

            // From RemoveTalaeaAddRegion migration
            try
            {
                await context.Database.ExecuteSqlRawAsync(
                    @"ALTER TABLE ""Members"" ADD COLUMN IF NOT EXISTS ""Region"" TEXT");
            }
            catch { /* safe */ }

            // From AddMemberNotes migration
            try
            {
                await context.Database.ExecuteSqlRawAsync(
                    @"ALTER TABLE ""Members"" ADD COLUMN IF NOT EXISTS ""Notes"" TEXT");
            }
            catch { /* safe */ }

            // From AddMemberGenderAndCustomId migration — CRITICAL for import
            // Gender is NOT NULL (1=Male default); CustomId is NOT NULL (0 placeholder).
            try
            {
                await context.Database.ExecuteSqlRawAsync(
                    @"ALTER TABLE ""Members"" ADD COLUMN IF NOT EXISTS ""Gender"" INTEGER NOT NULL DEFAULT 1");
            }
            catch { /* safe */ }
            try
            {
                await context.Database.ExecuteSqlRawAsync(
                    @"ALTER TABLE ""Members"" ADD COLUMN IF NOT EXISTS ""CustomId"" INTEGER NOT NULL DEFAULT 0");
            }
            catch { /* safe */ }

            // Backfill existing rows that still have CustomId = 0 (i.e. rows that existed
            // before the column was added) with sequential odd IDs starting from 100001.
            try
            {
                await context.Database.ExecuteSqlRawAsync(@"
                    WITH numbered AS (
                        SELECT ""Id"", ROW_NUMBER() OVER (ORDER BY ""CreatedAt"", ""Id"") AS rn
                        FROM   ""Members""
                        WHERE  ""CustomId"" = 0
                    )
                    UPDATE ""Members""
                    SET    ""CustomId"" = 100001 + ((numbered.rn - 1) * 2)
                    FROM   numbered
                    WHERE  ""Members"".""Id"" = numbered.""Id""");
            }
            catch { /* safe — no rows may need backfilling */ }

            // Unique index on CustomId (CREATE UNIQUE INDEX IF NOT EXISTS is safe to repeat)
            try
            {
                await context.Database.ExecuteSqlRawAsync(
                    @"CREATE UNIQUE INDEX IF NOT EXISTS ""IX_Members_CustomId"" ON ""Members"" (""CustomId"")");
            }
            catch { /* safe */ }

            // Unique index on QrCode (should already exist from EnsureCreated, but guard anyway)
            try
            {
                await context.Database.ExecuteSqlRawAsync(
                    @"CREATE UNIQUE INDEX IF NOT EXISTS ""IX_Members_QrCode"" ON ""Members"" (""QrCode"")");
            }
            catch { /* safe */ }

            // ProfileImageUrl — added in AddMemberProfileImageUrl migration
            try
            {
                await context.Database.ExecuteSqlRawAsync(
                    @"ALTER TABLE ""Members"" ADD COLUMN IF NOT EXISTS ""ProfileImageUrl"" TEXT");
            }
            catch { /* safe */ }

            // ── 3o. MemberExcuses table — create if it was added to the model AFTER
            //        the Railway DB was first created by EnsureCreated.
            //        EnsureCreated is idempotent for the whole DB but does NOT add
            //        individual tables that didn't exist at first-run time.
            try
            {
                // EF Core always supplies the Guid PK value client-side before INSERT,
                // so no DB-level DEFAULT is needed for "Id".  Avoiding gen_random_uuid()
                // keeps this compatible with PostgreSQL 12 and any Railway instance that
                // hasn't loaded the pgcrypto extension.
                await context.Database.ExecuteSqlRawAsync(@"
                    CREATE TABLE IF NOT EXISTS ""MemberExcuses"" (
                        ""Id""        UUID                     NOT NULL,
                        ""MemberId""  UUID                     NOT NULL,
                        ""StartDate"" TIMESTAMP WITH TIME ZONE NOT NULL,
                        ""EndDate""   TIMESTAMP WITH TIME ZONE,
                        ""Reason""    TEXT,
                        ""IsActive""  BOOLEAN                  NOT NULL DEFAULT TRUE,
                        ""GrantedBy"" UUID                     NOT NULL,
                        ""CreatedAt"" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
                        ""UpdatedAt"" TIMESTAMP WITH TIME ZONE,
                        ""IsDeleted"" BOOLEAN                  NOT NULL DEFAULT FALSE,
                        CONSTRAINT ""PK_MemberExcuses"" PRIMARY KEY (""Id""),
                        CONSTRAINT ""FK_MemberExcuses_Members_MemberId""
                            FOREIGN KEY (""MemberId"") REFERENCES ""Members""(""Id"") ON DELETE CASCADE
                    )");
            }
            catch { /* safe — table already exists (IF NOT EXISTS handles idempotency) */ }

            // Indexes for MemberExcuses (idempotent)
            try { await context.Database.ExecuteSqlRawAsync(@"CREATE INDEX IF NOT EXISTS ""IX_MemberExcuses_MemberId"" ON ""MemberExcuses"" (""MemberId"")"); } catch { }
            try { await context.Database.ExecuteSqlRawAsync(@"CREATE INDEX IF NOT EXISTS ""IX_MemberExcuses_MemberId_IsActive"" ON ""MemberExcuses"" (""MemberId"", ""IsActive"")"); } catch { }

            // ── ShareToken on Troops ──────────────────────────────────────────────
            try
            {
                await context.Database.ExecuteSqlRawAsync(
                    @"ALTER TABLE ""Troops"" ADD COLUMN IF NOT EXISTS ""ShareToken"" TEXT NOT NULL DEFAULT ''");
            }
            catch { /* safe */ }

            // Backfill existing troops that have an empty ShareToken
            // Using EF Core to load + update avoids gen_random_uuid() dependency.
            try
            {
                var troopsWithNoToken = await context.Troops
                    .IgnoreQueryFilters()
                    .Where(t => t.ShareToken == null || t.ShareToken == string.Empty)
                    .ToListAsync();

                foreach (var t in troopsWithNoToken)
                {
                    t.ShareToken = Guid.NewGuid().ToString("N");
                    context.Update(t);
                }
                if (troopsWithNoToken.Count > 0)
                    await context.SaveChangesAsync();
            }
            catch { /* safe — may fail if column not yet visible in this transaction */ }

            // Unique index on ShareToken (safe to repeat)
            try
            {
                await context.Database.ExecuteSqlRawAsync(
                    @"CREATE UNIQUE INDEX IF NOT EXISTS ""IX_Troops_ShareToken"" ON ""Troops"" (""ShareToken"") WHERE ""ShareToken"" <> ''");
            }
            catch { /* safe */ }

            // ── PendingExcuses table ──────────────────────────────────────────────
            try
            {
                await context.Database.ExecuteSqlRawAsync(@"
                    CREATE TABLE IF NOT EXISTS ""PendingExcuses"" (
                        ""Id""               UUID                     NOT NULL,
                        ""TroopId""          UUID                     NOT NULL,
                        ""SubmitterName""    TEXT                     NOT NULL DEFAULT '',
                        ""MemberName""       TEXT                     NOT NULL DEFAULT '',
                        ""MemberCustomId""   INTEGER,
                        ""StartDate""        TIMESTAMP WITH TIME ZONE NOT NULL,
                        ""EndDate""          TIMESTAMP WITH TIME ZONE NOT NULL,
                        ""Reason""           TEXT                     NOT NULL DEFAULT '',
                        ""SubmitterIp""      TEXT                     NOT NULL DEFAULT '',
                        ""Status""           INTEGER                  NOT NULL DEFAULT 0,
                        ""ReviewNotes""      TEXT,
                        ""ReviewedBy""       UUID,
                        ""ReviewedAt""       TIMESTAMP WITH TIME ZONE,
                        ""ResultingExcuseId"" UUID,
                        ""CreatedAt""        TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
                        ""UpdatedAt""        TIMESTAMP WITH TIME ZONE,
                        ""IsDeleted""        BOOLEAN                  NOT NULL DEFAULT FALSE,
                        CONSTRAINT ""PK_PendingExcuses"" PRIMARY KEY (""Id""),
                        CONSTRAINT ""FK_PendingExcuses_Troops_TroopId""
                            FOREIGN KEY (""TroopId"") REFERENCES ""Troops""(""Id"") ON DELETE RESTRICT
                    )");
            }
            catch { /* safe — IF NOT EXISTS handles idempotency */ }

            try { await context.Database.ExecuteSqlRawAsync(@"CREATE INDEX IF NOT EXISTS ""IX_PendingExcuses_TroopId"" ON ""PendingExcuses"" (""TroopId"")"); } catch { }
            try { await context.Database.ExecuteSqlRawAsync(@"CREATE INDEX IF NOT EXISTS ""IX_PendingExcuses_Status"" ON ""PendingExcuses"" (""Status"")"); } catch { }

            // ── 3q–3t. Events table point configuration columns ───────────────────
            // AddEventPointsConfig migration: rename PointValue→PresentPoints and
            // LatePointValue→LatePoints, then add ExcusedPoints and AbsentPoints.
            //
            // RENAME is NOT idempotent (errors if already renamed), so wrap each in its
            // own try/catch.  ADD COLUMN IF NOT EXISTS is inherently idempotent.
            try
            {
                await context.Database.ExecuteSqlRawAsync(
                    @"ALTER TABLE ""Events"" RENAME COLUMN ""PointValue"" TO ""PresentPoints""");
            }
            catch { /* already renamed or column doesn't exist — safe to ignore */ }

            try
            {
                await context.Database.ExecuteSqlRawAsync(
                    @"ALTER TABLE ""Events"" RENAME COLUMN ""LatePointValue"" TO ""LatePoints""");
            }
            catch { /* already renamed — safe */ }

            try
            {
                await context.Database.ExecuteSqlRawAsync(
                    @"ALTER TABLE ""Events"" ADD COLUMN IF NOT EXISTS ""ExcusedPoints"" DECIMAL(10,2) NOT NULL DEFAULT 50");
            }
            catch { /* safe */ }

            try
            {
                await context.Database.ExecuteSqlRawAsync(
                    @"ALTER TABLE ""Events"" ADD COLUMN IF NOT EXISTS ""AbsentPoints"" DECIMAL(10,2) NOT NULL DEFAULT -10");
            }
            catch { /* safe */ }
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
                DateOfBirth = new DateTime(2010, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            };
            m.QrCode = $"SCOUT-{customId}";
            members.Add(m);
        }
        await context.Members.AddRangeAsync(members);

        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Returns true ONLY when the exception indicates that a table (relation)
    /// does not exist in PostgreSQL — error code 42P01 "undefined_table".
    ///
    /// IMPORTANT: Do NOT match on the generic "does not exist" message string.
    /// PostgreSQL uses that phrase for missing columns, functions, types, etc.
    /// Matching it broadly caused EnsureDeletedAsync to fire on column-not-found
    /// errors and wipe all production data.  Use SqlState exclusively.
    /// </summary>
    private static bool IsUndefinedTableError(Exception ex)
    {
        for (var e = ex; e is not null; e = e.InnerException)
        {
            // Primary check: SqlState property exposed by NpgsqlException
            var sqlState = e.GetType().GetProperty("SqlState")?.GetValue(e) as string;
            if (sqlState == "42P01") return true;

            // Fallback: error code embedded in the message (e.g. "42P01: relation ... does not exist")
            // Only match when the 5-char SQLSTATE code itself appears in the message.
            if (e.GetType().Name == "PostgresException" && e.Message.Contains("42P01"))
                return true;
        }
        return false;
    }
}
