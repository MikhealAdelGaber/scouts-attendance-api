using ScoutsAttendance.Domain.Enums;

namespace ScoutsAttendance.Application.Interfaces;

/// <summary>
/// Generates unique 6-digit member IDs where:
///   Male   → odd  last digit  (100001, 100003, …)
///   Female → even last digit  (100002, 100004, …)
/// </summary>
public interface ICustomIdService
{
    /// <param name="gender">Determines odd (Male) or even (Female) ID.</param>
    /// <param name="excludeIds">
    ///     IDs already allocated in the current in-memory batch that have not yet been
    ///     persisted to the database. Pass this to avoid duplicate collisions during
    ///     bulk-import where multiple IDs are generated before a single SaveChanges.
    /// </param>
    Task<int> GenerateAsync(Gender gender, ISet<int>? excludeIds = null);
}
