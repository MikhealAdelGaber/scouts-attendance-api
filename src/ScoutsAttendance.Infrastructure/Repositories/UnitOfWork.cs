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

    public async Task<int> SaveChangesAsync() => await _context.SaveChangesAsync();
    public void Dispose() => _context.Dispose();
}
