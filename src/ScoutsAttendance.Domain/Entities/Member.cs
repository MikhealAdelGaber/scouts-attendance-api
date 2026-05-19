using ScoutsAttendance.Domain.Common;
using ScoutsAttendance.Domain.Enums;

namespace ScoutsAttendance.Domain.Entities;

public class Member : BaseEntity
{
    public string    FirstName      { get; set; } = string.Empty;
    public string    LastName       { get; set; } = string.Empty;
    public string?   PhoneNumber    { get; set; }
    public DateTime  DateOfBirth    { get; set; }
    public Guid?     TroopId        { get; set; }   // null = unassigned (troop was deleted)
    public Guid      GroupId        { get; set; }
    public Guid?     UserId         { get; set; }
    public string    QrCode         { get; set; } = string.Empty;

    /// <summary>Member gender — used to determine odd (Male) or even (Female) CustomId.</summary>
    public Gender    Gender         { get; set; } = Gender.Male;

    /// <summary>Auto-generated unique 6-digit ID. Odd last digit = Male, Even last digit = Female.</summary>
    public int       CustomId       { get; set; }

    // ─── Extended profile fields ──────────────────────────────────────────────
    public string?   Address        { get; set; }
    public string?   Region         { get; set; }   // المنطقة
    public bool      HasNeckerchief { get; set; } = false;
    public int?      YearJoined     { get; set; }
    public string?   AcademicYear   { get; set; }   // e.g. "Grade 5"
    public string?   FatherPhone    { get; set; }
    public string?   MotherPhone    { get; set; }
    public string?   Notes          { get; set; }   // Free-text notes
    public string?   ProfileImageUrl { get; set; }  // Cloudinary URL or local relative path

    // ─── Navigation ───────────────────────────────────────────────────────────
    public Troop?  Troop  { get; set; }
    public Group?  Group  { get; set; }
    public User?   User   { get; set; }

    public ICollection<AttendanceRecord> AttendanceRecords { get; set; } = new List<AttendanceRecord>();
    public ICollection<MemberPoints>     MemberPoints      { get; set; } = new List<MemberPoints>();
    public ICollection<MemberExcuse>     Excuses           { get; set; } = new List<MemberExcuse>();
    public ICollection<MemberExamScore>  ExamScores        { get; set; } = new List<MemberExamScore>();
    public ICollection<Transfer>         TransfersFrom     { get; set; } = new List<Transfer>();
    public ICollection<Transfer>         TransfersTo       { get; set; } = new List<Transfer>();

    public string FullName => $"{FirstName} {LastName}";

    /// <summary>
    /// Returns true if the member has an active excuse whose date range covers
    /// <paramref name="date"/>.  The comparison is date-only (time-of-day is ignored)
    /// so that UTC event timestamps are correctly matched against excuse start/end dates.
    /// </summary>
    public bool HasActiveExcuse(DateTime date)
    {
        var d = date.Date;   // strip time component
        return Excuses.Any(e => !e.IsDeleted && e.IsActive &&
                                 e.StartDate.Date <= d &&
                                 (e.EndDate == null || e.EndDate.Value.Date >= d));
    }
}
