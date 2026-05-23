using System.Collections.Concurrent;
using ScoutsAttendance.Application.Interfaces;

namespace ScoutsAttendance.Infrastructure.Services;

/// <summary>
/// Thread-safe in-memory rate limiter registered as a singleton.
/// Tracks request timestamps per IP; prunes old entries on each check
/// so memory usage stays bounded to active submitters.
/// </summary>
public class InMemoryIpRateLimiter : IIpRateLimiter
{
    // IP → list of UTC timestamps of recent requests
    private readonly ConcurrentDictionary<string, List<DateTime>> _timestamps = new();
    private readonly object _lock = new();

    public bool IsAllowed(string ip, int maxRequests, TimeSpan window)
    {
        var now = DateTime.UtcNow;
        var cutoff = now - window;

        lock (_lock)
        {
            if (!_timestamps.TryGetValue(ip, out var times))
            {
                times = new List<DateTime>();
                _timestamps[ip] = times;
            }

            // Prune timestamps outside the window
            times.RemoveAll(t => t < cutoff);

            if (times.Count >= maxRequests)
                return false;

            times.Add(now);
            return true;
        }
    }
}
