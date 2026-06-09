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
    public DbSet<ExamScoreConfig>  ExamScoreConfigs  => Set<ExamScoreConfig>();
    public DbSet<PendingExcuse>    PendingExcuses    => Set<PendingExcuse>();

    // ── Trips ─────────────────────────────────────────────────────────────────
    public DbSet<Trip>                 Trips                 => Set<Trip>();
    public DbSet<TripBooking>          TripBookings          => Set<TripBooking>();
    public DbSet<TripAttendanceRecord> TripAttendanceRecords => Set<TripAttendanceRecord>();
    public DbSet<BookingPayment>       BookingPayments       => Set<BookingPayment>();

    // ── Badges ────────────────────────────────────────────────────────────────
    public DbSet<Badge>       Badges       => Set<Badge>();
    public DbSet<MemberBadge> MemberBadges => Set<MemberBadge>();

    // ── Projects ──────────────────────────────────────────────────────────────
    public DbSet<Project>            Projects      => Set<Project>();
    public DbSet<MemberProjectScore> ProjectScores => Set<MemberProjectScore>();

    // ── Final Report ──────────────────────────────────────────────────────────
    public DbSet<ReportTemplate>         ReportTemplates   => Set<ReportTemplate>();
    public DbSet<ReportTemplateCategory> ReportCategories  => Set<ReportTemplateCategory>();
    public DbSet<MemberCustomScore>      CustomScores      => Set<MemberCustomScore>();

    // ── Transfer Requests ─────────────────────────────────────────────────────
    public DbSet<MemberTransferRequest>  TransferRequests  => Set<MemberTransferRequest>();
    public DbSet<MemberTransferArchive>  TransferArchives  => Set<MemberTransferArchive>();

    // ── Yearly Archives ───────────────────────────────────────────────────────
    public DbSet<YearlyArchive>       YearlyArchives       => Set<YearlyArchive>();
    public DbSet<YearlyMemberArchive> YearlyMemberArchives => Set<YearlyMemberArchive>();

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
            // Badges permission defaults false — opt-in for non-admin roles
            e.Property(u => u.CanAccessBadges).HasDefaultValue(false);
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
            // HasDefaultValue/HasDefaultValueSql both set ValueGeneratedOnAdd, causing EF Core
            // to skip the column in INSERT when value == CLR default (0).  This means saving
            // TooLatePoints=0 or ExcusedPoints=0 would silently revert to the DB default.
            // Fix: declare type only — EF Core always includes the column in INSERT/UPDATE.
            // C# entity-level defaults (= 100m, = 50m …) provide the correct fallback.
            e.Property(ev => ev.PresentPoints).HasColumnType("decimal(10,2)");
            e.Property(ev => ev.LatePoints).HasColumnType("decimal(10,2)");
            e.Property(ev => ev.ExcusedPoints).HasColumnType("decimal(10,2)");
            e.Property(ev => ev.AbsentPoints).HasColumnType("decimal(10,2)");
            e.Property(ev => ev.TooLatePoints).HasColumnType("decimal(10,2)");
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
            e.Property(x => x.TheoreticalScore).HasColumnType("decimal(8,2)").HasDefaultValue(0m);
            e.Property(x => x.PracticalScore).HasColumnType("decimal(8,2)").HasDefaultValue(0m);
            e.HasOne(x => x.Member)
                .WithMany(m => m.ExamScores)
                .HasForeignKey(x => x.MemberId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ─── ExamScoreConfig ───────────────────────────────────────────────────
        mb.Entity<ExamScoreConfig>(e =>
        {
            e.ToTable("ExamScoreConfigs");
            e.HasIndex(x => new { x.GroupId, x.Year }).IsUnique();
            e.Property(x => x.TheoreticalMaxScore).HasColumnType("decimal(8,2)").HasDefaultValue(50m);
            e.Property(x => x.PracticalMaxScore).HasColumnType("decimal(8,2)").HasDefaultValue(50m);
            e.Property(x => x.CreatedBy).HasMaxLength(200);
            e.HasOne(x => x.Group)
                .WithMany()
                .HasForeignKey(x => x.GroupId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        mb.Entity<ExamScoreConfig>().HasQueryFilter(e => !e.IsDeleted);

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

        // ─── Badge ─────────────────────────────────────────────────────────────
        mb.Entity<Badge>(e =>
        {
            e.HasIndex(b => b.Name);
            e.HasIndex(b => b.Category);
        });
        mb.Entity<Badge>().HasQueryFilter(e => !e.IsDeleted);

        // ─── MemberTransferRequest ─────────────────────────────────────────────
        mb.Entity<MemberTransferRequest>(e =>
        {
            // DbSet property is "TransferRequests" but the physical table is
            // "MemberTransferRequests" (created by DbSeeder + EnsureCreated).
            // Without this override EF Core would look for a "TransferRequests"
            // table that doesn't exist, causing every write to fail.
            e.ToTable("MemberTransferRequests");
            e.HasIndex(r => r.MemberId);
            e.HasIndex(r => r.FromGroupId);
            e.HasIndex(r => r.ToGroupId);
            e.HasIndex(r => r.Status);
            e.Property(r => r.Status).HasConversion<int>();
            e.Property(r => r.MemberName).HasMaxLength(200);
            e.Property(r => r.FromGroupName).HasMaxLength(200);
            e.Property(r => r.ToGroupName).HasMaxLength(200);
            e.Property(r => r.RequestedBy).HasMaxLength(200);
            e.Property(r => r.ReviewedBy).HasMaxLength(200).IsRequired(false);
            e.Property(r => r.RejectionReason).HasMaxLength(500).IsRequired(false);
            e.Property(r => r.Notes).HasMaxLength(500).IsRequired(false);
            e.HasOne(r => r.Member)
                .WithMany()
                .HasForeignKey(r => r.MemberId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(r => r.FromGroup)
                .WithMany()
                .HasForeignKey(r => r.FromGroupId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(r => r.ToGroup)
                .WithMany()
                .HasForeignKey(r => r.ToGroupId)
                .OnDelete(DeleteBehavior.Restrict);
        });
        mb.Entity<MemberTransferRequest>().HasQueryFilter(e => !e.IsDeleted);

        // ─── MemberBadge ───────────────────────────────────────────────────────
        mb.Entity<MemberBadge>(e =>
        {
            e.HasIndex(mb => mb.MemberId);
            e.HasIndex(mb => mb.BadgeId);
            // TroopName is a denormalised snapshot — nullable, no FK constraint
            e.Property(mb => mb.TroopName).HasMaxLength(200).IsRequired(false);
            e.HasOne(mb => mb.Member)
                .WithMany()
                .HasForeignKey(mb => mb.MemberId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(mb => mb.Badge)
                .WithMany(b => b.MemberBadges)
                .HasForeignKey(mb => mb.BadgeId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(mb => mb.Troop)
                .WithMany()
                .HasForeignKey(mb => mb.TroopId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);
        });
        mb.Entity<MemberBadge>().HasQueryFilter(e => !e.IsDeleted);

        // ─── YearlyArchive ─────────────────────────────────────────────────────
        mb.Entity<YearlyArchive>(e =>
        {
            e.ToTable("YearlyArchives");
            e.HasIndex(a => a.ArchiveYear);
            e.Property(a => a.ArchiveYear).HasMaxLength(20);
            e.Property(a => a.ArchivedBy).HasMaxLength(200);
        });
        mb.Entity<YearlyArchive>().HasQueryFilter(e => !e.IsDeleted);

        // ─── YearlyMemberArchive ───────────────────────────────────────────────
        mb.Entity<YearlyMemberArchive>(e =>
        {
            e.ToTable("YearlyMemberArchives");
            e.HasIndex(a => a.YearlyArchiveId);
            e.HasIndex(a => a.MemberId);
            e.HasIndex(a => a.GroupId);
            e.Property(a => a.MemberName).HasMaxLength(200);
            e.Property(a => a.GroupName).HasMaxLength(200);
            e.Property(a => a.TroopName).HasMaxLength(200).IsRequired(false);
            e.Property(a => a.AcademicGrade).HasMaxLength(100).IsRequired(false);
            e.Property(a => a.TotalPointsAtYearEnd).HasColumnType("decimal(10,2)");
            e.HasOne(a => a.YearlyArchive)
                .WithMany(y => y.Members)
                .HasForeignKey(a => a.YearlyArchiveId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        mb.Entity<YearlyMemberArchive>().HasQueryFilter(e => !e.IsDeleted);

        // ─── MemberTransferArchive ─────────────────────────────────────────────
        mb.Entity<MemberTransferArchive>(e =>
        {
            e.ToTable("MemberTransferArchives");
            e.HasIndex(a => a.MemberId);
            e.HasIndex(a => a.FromGroupId);
            e.HasIndex(a => a.ToGroupId);
            e.Property(a => a.MemberName).HasMaxLength(200);
            e.Property(a => a.FromGroupName).HasMaxLength(200);
            e.Property(a => a.ToGroupName).HasMaxLength(200);
            e.Property(a => a.TotalPointsAtTransfer).HasColumnType("decimal(10,2)");
        });
        mb.Entity<MemberTransferArchive>().HasQueryFilter(e => !e.IsDeleted);

        // ─── ReportTemplate ────────────────────────────────────────────────────
        mb.Entity<ReportTemplate>(e =>
        {
            e.ToTable("ReportTemplates");
            e.HasIndex(t => t.GroupId);
            e.Property(t => t.Name).HasMaxLength(200);
            e.Property(t => t.CreatedBy).HasMaxLength(200);
            e.HasOne(t => t.Group).WithMany().HasForeignKey(t => t.GroupId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(t => t.Troop).WithMany().HasForeignKey(t => t.TroopId).IsRequired(false).OnDelete(DeleteBehavior.SetNull);
        });
        mb.Entity<ReportTemplate>().HasQueryFilter(e => !e.IsDeleted);

        // ─── ReportTemplateCategory ────────────────────────────────────────────
        mb.Entity<ReportTemplateCategory>(e =>
        {
            e.ToTable("ReportTemplateCategories");
            e.HasIndex(c => c.ReportTemplateId);
            e.Property(c => c.CategoryName).HasMaxLength(200);
            e.Property(c => c.CustomDescription).HasMaxLength(500).IsRequired(false);
            e.Property(c => c.Weight).HasColumnType("decimal(10,2)");
            e.Property(c => c.CategoryType).HasConversion<int>();
            e.HasOne(c => c.Template).WithMany(t => t.Categories).HasForeignKey(c => c.ReportTemplateId).OnDelete(DeleteBehavior.Cascade);
        });
        mb.Entity<ReportTemplateCategory>().HasQueryFilter(e => !e.IsDeleted);

        // ─── MemberCustomScore ─────────────────────────────────────────────────
        mb.Entity<MemberCustomScore>(e =>
        {
            e.ToTable("MemberCustomScores");
            e.HasIndex(s => new { s.ReportTemplateCategoryId, s.MemberId }).IsUnique();
            e.HasIndex(s => s.MemberId);
            e.Property(s => s.Score).HasColumnType("decimal(10,2)");
            e.Property(s => s.Notes).HasMaxLength(500).IsRequired(false);
            e.Property(s => s.EnteredBy).HasMaxLength(200);
            e.HasOne(s => s.Category).WithMany(c => c.CustomScores).HasForeignKey(s => s.ReportTemplateCategoryId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(s => s.Member).WithMany().HasForeignKey(s => s.MemberId).OnDelete(DeleteBehavior.Cascade);
        });
        mb.Entity<MemberCustomScore>().HasQueryFilter(e => !e.IsDeleted);

        // ─── Project ───────────────────────────────────────────────────────────
        mb.Entity<Project>(e =>
        {
            e.ToTable("Projects");
            e.HasIndex(p => p.GroupId);
            e.HasIndex(p => p.TroopId);
            e.Property(p => p.Name).HasMaxLength(200);
            e.Property(p => p.Description).HasMaxLength(1000).IsRequired(false);
            e.Property(p => p.MaxScore).HasColumnType("decimal(10,2)");
            e.Property(p => p.CreatedBy).HasMaxLength(200);
            e.HasOne(p => p.Group)
                .WithMany()
                .HasForeignKey(p => p.GroupId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(p => p.Troop)
                .WithMany()
                .HasForeignKey(p => p.TroopId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);
        });
        mb.Entity<Project>().HasQueryFilter(e => !e.IsDeleted);

        // ─── MemberProjectScore ────────────────────────────────────────────────
        mb.Entity<MemberProjectScore>(e =>
        {
            e.ToTable("MemberProjectScores");
            e.HasIndex(s => new { s.ProjectId, s.MemberId }).IsUnique();
            e.HasIndex(s => s.MemberId);
            e.Property(s => s.Score).HasColumnType("decimal(10,2)");
            e.Property(s => s.Notes).HasMaxLength(500).IsRequired(false);
            e.Property(s => s.GradedBy).HasMaxLength(200);
            e.HasOne(s => s.Project)
                .WithMany(p => p.Scores)
                .HasForeignKey(s => s.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(s => s.Member)
                .WithMany()
                .HasForeignKey(s => s.MemberId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        mb.Entity<MemberProjectScore>().HasQueryFilter(e => !e.IsDeleted);
    }
}
