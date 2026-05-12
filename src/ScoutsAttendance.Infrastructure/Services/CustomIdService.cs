using Microsoft.EntityFrameworkCore;
using ScoutsAttendance.Application.Interfaces;
using ScoutsAttendance.Domain.Enums;
using ScoutsAttendance.Infrastructure.Data;

namespace ScoutsAttendance.Infrastructure.Services;

/// <summary>
/// Generates unique 6-digit member IDs.
/// Male   → odd  last digit  (100001, 100003, 100005 …)
/// Female → even last digit  (100002, 100004, 100006 …)
/// </summary>
public class CustomIdService : ICustomIdService
{
    private readonly ApplicationDbContext _db;

    public CustomIdService(ApplicationDbContext db) => _db = db;

    public async Task<int> GenerateAsync(Gender gender, ISet<int>? excludeIds = null)
    {
        const int maxAttempts = 50;   // raised: large batches need more headroom

        // Build the full set of "taken" IDs once: DB + in-memory batch
        var dbIds = await _db.Members
            .IgnoreQueryFilters()     // include soft-deleted so IDs are never recycled
            .Where(m => m.CustomId > 0)
            .Select(m => m.CustomId)
            .ToListAsync();

        var takenIds = new HashSet<int>(dbIds);
        if (excludeIds is not null)
            takenIds.UnionWith(excludeIds);

        // Walk forward from the current max in steps of 2 until we find a free slot
        int step = 2;
        int seed = gender == Gender.Male
            ? (takenIds.Where(id => id % 2 != 0).DefaultIfEmpty(99999).Max() + step)
            : (takenIds.Where(id => id % 2 == 0).DefaultIfEmpty(100000).Max() + step);

        // Ensure correct parity after seed calculation
        if (gender == Gender.Male  && seed % 2 == 0) seed++;
        if (gender == Gender.Female && seed % 2 != 0) seed++;

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            int candidate = seed + (attempt * step);
            if (!takenIds.Contains(candidate))
                return candidate;
        }

        throw new InvalidOperationException(
            $"Failed to generate a unique CustomId for {gender} after {maxAttempts} attempts.");
    }

}
