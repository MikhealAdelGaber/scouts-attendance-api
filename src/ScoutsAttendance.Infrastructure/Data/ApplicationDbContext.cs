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
    }
}
