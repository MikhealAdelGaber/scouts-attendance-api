using Microsoft.EntityFrameworkCore;
using ScoutsAttendance.Domain.Entities;

namespace ScoutsAttendance.Infrastructure.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<User>             Users             => Set<User>();
    public DbSet<Group>            Groups            => Set<Group>();
    public DbSet<Troop>            Troops            => Set<Troop>();
    public DbSet<Member>           Members           => Set<Member>();
    public DbSet<Event>            Events            => Set<Event>();
    public DbSet<AttendanceRecord> AttendanceRecords => Set<AttendanceRecord>();
    public DbSet<PointCategory>       PointCategories       => Set<PointCategory>();
    public DbSet<MemberPointCategory> MemberPointCategories => Set<MemberPointCategory>();
    public DbSet<TroopPointCategory>  TroopPointCategories  => Set<TroopPointCategory>();
    public DbSet<MemberPoints>        MemberPoints          => Set<MemberPoints>();
    public DbSet<TroopPoints>         TroopPoints           => Set<TroopPoints>();
    public DbSet<Transfer>         Transfers         => Set<Transfer>();
    public DbSet<MemberExcuse>     MemberExcuses     => Set<MemberExcuse>();
    public DbSet<MemberExamScore>  MemberExamScores  => Set<MemberExamScore>();
    public DbSet<PendingExcuse>    PendingExcuses    => Set<PendingExcuse>();

    // ── Trips ─────────────────────────────────────────────────────────────────
    public DbSet<Trip>                 Trips                 => Set<Trip>();
    public DbSet<TripBooking>          TripBookings          => Set<TripBooking>();
    public DbSet<TripAttendanceRecord> TripAttendanceRecords => Set<TripAttendanceRecord>();
    public DbSet<BookingPayment>       BookingPayments       => Set<BookingPayment>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        base.OnModelCreating(mb);

        // ─── User ──────────────────────────────────────────────────────────────
        mb.Entity<User>(e =>
        {
            e.HasIndex(u => u.Username).IsUnique();
            e.HasIndex(u => u.Email).IsUnique();
            e.HasIndex(u => u.GroupId);
            e.HasIndex(u => u.TroopId);
            e.Property(u => u.Role).HasConversion<int>();
            e.Property(u => u.CanTakeAttendance).HasDefaultValue(false);
            e.Property(u => u.CanEditMembers).HasDefaultValue(false);
            e.Property(u => u.CanCreateEvents).HasDefaultValue(false);
            // Page-access permissions default to true so existing users are not locked out
            e.Property(u => u.CanAccessDashboard).HasDefaultValue(true);
            e.Property(u => u.CanAccessTroops).HasDefaultValue(true);
            e.Property(u => u.CanAccessMembers).HasDefaultValue(true);
            e.Property(u => u.CanAccessExcuses).HasDefaultValue(true);
            e.Property(u => u.CanAccessEvents).HasDefaultValue(true);
            e.Property(u => u.CanAccessAttendance).HasDefaultValue(true);
            e.Property(u => u.CanAccessPoints).HasDefaultValue(true);
            e.Property(u => u.CanAccessLeaderboard).HasDefaultValue(true);
            e.Property(u => u.CanAccessExamScores).HasDefaultValue(true);
            e.Property(u => u.CanAccessReports).HasDefaultValue(true);
        });

        // ─── Group ─────────────────────────────────────────────────────────────
        mb.Entity<Group>(e =>
        {
            e.HasOne(g => g.Leader)
                .WithMany()
                .HasForeignKey(g => g.LeaderId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ─── Troop ─────────────────────────────────────────────────────────────
        mb.Entity<Troop>(e =>
        {
            e.HasIndex(t => t.GroupId);
            e.HasIndex(t => t.ShareToken).IsUnique();
            e.HasOne(t => t.Group)
                .WithMany(g => g.Troops)
                .HasForeignKey(t => t.GroupId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(t => t.Leader)
                .WithMany()
                .HasForeignKey(t => t.LeaderId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // ─── Member ────────────────────────────────────────────────────────────
        mb.Entity<Member>(e =>
        {
            e.HasIndex(m => m.GroupId);
            e.HasIndex(m => m.TroopId);
            e.HasIndex(m => m.QrCode).IsUnique();
            e.HasIndex(m => m.CustomId).IsUnique();
            // Composite index for the fast-search endpoint (GroupId + names)
            e.HasIndex(m => new { m.GroupId, m.FirstName, m.LastName })
             .HasDatabaseName("IX_Members_GroupId_FirstName_LastName");
            e.HasIndex(m => new { m.GroupId, m.LastName, m.FirstName })
             .HasDatabaseName("IX_Members_GroupId_LastName_FirstName");
            e.Property(m => m.Gender).HasConversion<int>();
            e.Ignore(m => m.FullName);
            e.HasOne(m => m.Troop)
                .WithMany(t => t.Members)
                .HasForeignKey(m => m.TroopId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);
            e.HasOne(m => m.Group)
                .WithMany(g => g.Members)
                .HasForeignKey(m => m.GroupId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(m => m.User)
                .WithOne(u => u.Member)
                .HasForeignKey<Member>(m => m.UserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // ─── MemberExcuse ──────────────────────────────────────────────────────
        mb.Entity<MemberExcuse>(e =>
        {
            e.HasIndex(x => x.MemberId);
            e.HasIndex(x => new { x.MemberId, x.IsActive });
            e.HasOne(x => x.Member)
                .WithMany(m => m.Excuses)
                .HasForeignKey(x => x.MemberId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ─── Event ─────────────────────────────────────────────────────────────
        mb.Entity<Event>(e =>
        {
            e.HasIndex(ev => ev.GroupId);
            e.Property(ev => ev.PresentPoints).HasColumnType("decimal(10,2)").HasDefaultValue(100m);
            e.Property(ev => ev.LatePoints).HasColumnType("decimal(10,2)").HasDefaultValue(50m);
            e.Property(ev => ev.ExcusedPoints).HasColumnType("decimal(10,2)").HasDefaultValue(50m);
            e.Property(ev => ev.AbsentPoints).HasColumnType("decimal(10,2)").HasDefaultValue(-10m);
            e.HasOne(ev => ev.Group)
                .WithMany(g => g.Events)
                .HasForeignKey(ev => ev.GroupId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(ev => ev.Troop)
                .WithMany()
                .HasForeignKey(ev => ev.TroopId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // ─── AttendanceRecord ──────────────────────────────────────────────────
        mb.Entity<AttendanceRecord>(e =>
        {
            e.HasIndex(a => new { a.EventId, a.MemberId }).IsUnique();
            e.HasIndex(a => a.MemberId);
            e.Property(a => a.Status).HasConversion<int>();
            e.HasOne(a => a.Event)
                .WithMany(ev => ev.AttendanceRecords)
                .HasForeignKey(a => a.EventId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(a => a.Member)
                .WithMany(m => m.AttendanceRecords)
                .HasForeignKey(a => a.MemberId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(a => a.AutoPoints)
                .WithOne(mp => mp.AttendanceRecord)
                .HasForeignKey<MemberPoints>(mp => mp.AttendanceRecordId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // ─── PointCategory (legacy) ────────────────────────────────────────────
        mb.Entity<PointCategory>(e =>
        {
            e.HasIndex(pc => pc.GroupId);
            e.Property(pc => pc.AttendancePresentPoints).HasColumnType("decimal(10,2)");
            e.Property(pc => pc.AttendanceLatePoints).HasColumnType("decimal(10,2)");
            e.HasOne(pc => pc.Group)
                .WithMany(g => g.PointCategories)
                .HasForeignKey(pc => pc.GroupId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // ─── MemberPointCategory ───────────────────────────────────────────────
        mb.Entity<MemberPointCategory>(e =>
        {
            e.HasIndex(c => c.GroupId);
            e.Property(c => c.AttendancePresentPoints).HasColumnType("decimal(10,2)");
            e.Property(c => c.AttendanceLatePoints).HasColumnType("decimal(10,2)");
            e.HasOne(c => c.Group)
                .WithMany(g => g.MemberPointCategories)
                .HasForeignKey(c => c.GroupId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // ─── TroopPointCategory ────────────────────────────────────────────────
        mb.Entity<TroopPointCategory>(e =>
        {
            e.HasIndex(c => c.GroupId);
            e.HasOne(c => c.Group)
                .WithMany(g => g.TroopPointCategories)
                .HasForeignKey(c => c.GroupId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // ─── MemberPoints ──────────────────────────────────────────────────────
        mb.Entity<MemberPoints>(e =>
        {
            e.HasIndex(mp => mp.MemberId);
            e.HasIndex(mp => mp.MemberPointCategoryId);
            e.HasIndex(mp => mp.Date);
            e.Property(mp => mp.Points).HasColumnType("decimal(10,2)");
            e.HasOne(mp => mp.Member)
                .WithMany(m => m.MemberPoints)
                .HasForeignKey(mp => mp.MemberId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(mp => mp.Category)
                .WithMany(c => c.MemberPoints)
                .HasForeignKey(mp => mp.MemberPointCategoryId)
                .OnDelete(DeleteBehavior.SetNull)
                .IsRequired(false);
        });

        // ─── TroopPoints ───────────────────────────────────────────────────────
        mb.Entity<TroopPoints>(e =>
        {
            e.HasIndex(tp => tp.TroopId);
            e.HasIndex(tp => tp.TroopPointCategoryId);
            e.HasIndex(tp => tp.Date);
            e.Property(tp => tp.Points).HasColumnType("decimal(10,2)");
            e.HasOne(tp => tp.Troop)
                .WithMany(t => t.TroopPoints)
                .HasForeignKey(tp => tp.TroopId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(tp => tp.Category)
                .WithMany(c => c.TroopPoints)
                .HasForeignKey(tp => tp.TroopPointCategoryId)
                .OnDelete(DeleteBehavior.SetNull)
                .IsRequired(false);
        });

        // ─── Transfer ──────────────────────────────────────────────────────────
        mb.Entity<Transfer>(e =>
        {
            e.HasIndex(t => t.MemberId);
            e.HasOne(t => t.Member)
                .WithMany(m => m.TransfersFrom)
                .HasForeignKey(t => t.MemberId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(t => t.FromTroop)
                .WithMany()
                .HasForeignKey(t => t.FromTroopId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(t => t.ToTroop)
                .WithMany()
                .HasForeignKey(t => t.ToTroopId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ─── PendingExcuse ─────────────────────────────────────────────────────
        mb.Entity<PendingExcuse>(e =>
        {
            e.HasIndex(p => p.TroopId);
            e.HasIndex(p => p.MemberId);
            e.HasIndex(p => p.Status);
            e.Property(p => p.Status).HasConversion<int>();
            e.HasOne(p => p.Troop)
                .WithMany()
                .HasForeignKey(p => p.TroopId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(p => p.Member)
                .WithMany()
                .HasForeignKey(p => p.MemberId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ─── MemberExamScore ───────────────────────────────────────────────────
        mb.Entity<MemberExamScore>(e =>
        {
            e.HasIndex(x => x.MemberId);
            e.HasIndex(x => new { x.MemberId, x.Year }).IsUnique();
            e.Property(x => x.Score).HasColumnType("decimal(5,2)");
            e.HasOne(x => x.Member)
                .WithMany(m => m.ExamScores)
                .HasForeignKey(x => x.MemberId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ─── Global soft-delete query filters ──────────────────────────────────
        mb.Entity<User>().HasQueryFilter(e => !e.IsDeleted);
        mb.Entity<Group>().HasQueryFilter(e => !e.IsDeleted);
        mb.Entity<Troop>().HasQueryFilter(e => !e.IsDeleted);
        mb.Entity<Member>().HasQueryFilter(e => !e.IsDeleted);
        mb.Entity<Event>().HasQueryFilter(e => !e.IsDeleted);
        mb.Entity<AttendanceRecord>().HasQueryFilter(e => !e.IsDeleted);
        mb.Entity<PointCategory>().HasQueryFilter(e => !e.IsDeleted);
        mb.Entity<MemberPointCategory>().HasQueryFilter(e => !e.IsDeleted);
        mb.Entity<TroopPointCategory>().HasQueryFilter(e => !e.IsDeleted);
        mb.Entity<MemberPoints>().HasQueryFilter(e => !e.IsDeleted);
        mb.Entity<TroopPoints>().HasQueryFilter(e => !e.IsDeleted);
        mb.Entity<Transfer>().HasQueryFilter(e => !e.IsDeleted);
        mb.Entity<MemberExcuse>().HasQueryFilter(e => !e.IsDeleted);
        mb.Entity<MemberExamScore>().HasQueryFilter(e => !e.IsDeleted);
        mb.Entity<PendingExcuse>().HasQueryFilter(e => !e.IsDeleted);

        // ─── Trip ──────────────────────────────────────────────────────────────
        mb.Entity<Trip>(e =>
        {
            e.HasIndex(t => t.GroupId);
            e.HasIndex(t => t.Status);
            e.Property(t => t.Status).HasConversion<int>();
            e.Property(t => t.Price).HasColumnType("decimal(10,2)");
            e.Property(t => t.SiblingPrice).HasColumnType("decimal(10,2)");
            e.HasOne(t => t.Group)
                .WithMany()
                .HasForeignKey(t => t.GroupId)
                .OnDelete(DeleteBehavior.Restrict);
        });
        mb.Entity<Trip>().HasQueryFilter(e => !e.IsDeleted);

        // ─── TripBooking ───────────────────────────────────────────────────────
        mb.Entity<TripBooking>(e =>
        {
            e.HasIndex(b => new { b.TripId, b.MemberId }).IsUnique();
            e.HasIndex(b => b.BookingStatus);
            e.Property(b => b.BookingStatus).HasConversion<int>();
            e.Property(b => b.AmountDue).HasColumnType("decimal(10,2)");
            e.HasOne(b => b.Trip)
                .WithMany(t => t.Bookings)
                .HasForeignKey(b => b.TripId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(b => b.Member)
                .WithMany()
                .HasForeignKey(b => b.MemberId)
                .OnDelete(DeleteBehavior.Restrict);
        });
        mb.Entity<TripBooking>().HasQueryFilter(e => !e.IsDeleted);

        // ─── TripAttendanceRecord ──────────────────────────────────────────────
        mb.Entity<TripAttendanceRecord>(e =>
        {
            e.HasIndex(r => new { r.TripId, r.MemberId }).IsUnique();
            e.HasOne(r => r.Trip)
                .WithMany(t => t.AttendanceRecords)
                .HasForeignKey(r => r.TripId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(r => r.Member)
                .WithMany()
                .HasForeignKey(r => r.MemberId)
                .OnDelete(DeleteBehavior.Restrict);
        });
        mb.Entity<TripAttendanceRecord>().HasQueryFilter(e => !e.IsDeleted);

        // ─── BookingPayment ────────────────────────────────────────────────────────
        mb.Entity<BookingPayment>(e =>
        {
            e.HasIndex(p => p.BookingId);
            e.Property(p => p.AmountPaid).HasColumnType("decimal(10,2)");
            e.HasOne(p => p.Booking)
                .WithMany(b => b.Payments)
                .HasForeignKey(p => p.BookingId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        mb.Entity<BookingPayment>().HasQueryFilter(e => !e.IsDeleted);
    }
}
