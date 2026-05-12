using Microsoft.EntityFrameworkCore;
using ScoutsAttendance.Application.Interfaces;
using ScoutsAttendance.Domain.Enums;
using System.Text;

namespace ScoutsAttendance.Application.Services;

public interface IExportService
{
    /// <summary>Export attendance for an event (or date range) as CSV bytes.</summary>
    Task<byte[]> ExportAttendanceCsvAsync(Guid? eventId, Guid? troopId, DateTime? from, DateTime? to);
}

public class ExportService : IExportService
{
    private readonly IUnitOfWork _uow;

    public ExportService(IUnitOfWork uow) => _uow = uow;

    public async Task<byte[]> ExportAttendanceCsvAsync(
        Guid? eventId, Guid? troopId, DateTime? from, DateTime? to)
    {
        var query = _uow.AttendanceRecords.Query()
            .Include(a => a.Event)
            .Include(a => a.Member).ThenInclude(m => m.Troop)
            .Where(a => !a.IsDeleted);

        if (eventId.HasValue)  query = query.Where(a => a.EventId == eventId.Value);
        if (troopId.HasValue)  query = query.Where(a => a.Member.TroopId == troopId.Value);
        if (from.HasValue)     query = query.Where(a => a.Event.EventDate >= from.Value);
        if (to.HasValue)       query = query.Where(a => a.Event.EventDate <= to.Value);

        var records = await query.OrderBy(a => a.Event.EventDate)
                                 .ThenBy(a => a.Member.LastName)
                                 .ToListAsync();

        var sb = new StringBuilder();
        sb.AppendLine("Event,Event Date,Member,Troop,Status,Notes,Points Awarded,Marked At");

        foreach (var r in records)
        {
            sb.AppendLine(string.Join(',',
                Escape(r.Event?.Name),
                r.Event?.EventDate.ToString("yyyy-MM-dd"),
                Escape(r.Member?.FullName),
                Escape(r.Member?.Troop?.Name),
                r.Status.ToString(),
                Escape(r.Notes),
                r.AutoPoints?.Points.ToString("F1") ?? "0",
                r.MarkedAt.ToString("yyyy-MM-dd HH:mm")));
        }

        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    private static string Escape(string? s) =>
        s is null ? "" : $"\"{s.Replace("\"", "\"\"")}\"";
}
