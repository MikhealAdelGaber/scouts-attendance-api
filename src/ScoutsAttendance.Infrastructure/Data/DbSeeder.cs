using Microsoft.EntityFrameworkCore;
using ScoutsAttendance.Domain.Entities;
using ScoutsAttendance.Domain.Enums;

namespace ScoutsAttendance.Infrastructure.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(ApplicationDbContext context)
    {
        await context.Database.MigrateAsync();

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
        var names = new[] { ("Ahmed", "Hassan"), ("Sara", "Mohamed"), ("Omar", "Ali"), ("Nour", "Ibrahim"), ("Youssef", "Khaled") };
        foreach (var (first, last) in names)
        {
            var m = new Member
            {
                FirstName = first,
                LastName = last,
                TroopId = troop.Id,
                GroupId = group.Id,
                DateOfBirth = new DateTime(2010, 1, 1)
            };
            m.QrCode = $"SCOUT-{m.Id:N}";
            members.Add(m);
        }
        await context.Members.AddRangeAsync(members);

        await context.SaveChangesAsync();
    }
}
