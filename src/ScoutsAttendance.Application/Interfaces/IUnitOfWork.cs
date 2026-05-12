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

    Task<int> SaveChangesAsync();
}
