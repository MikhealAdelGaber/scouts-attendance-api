namespace ScoutsAttendance.Application.Interfaces;

/// <summary>
/// In-memory per-IP rate limiter for public (unauthenticated) endpoints.
/// </summary>
public interface IIpRateLimiter
{
    /// <summary>
    /// Returns true if the given IP is allowed to make a request.
    /// Tracks the request timestamp internally; call this once per incoming request.
    /// </summary>
    /// <param name="ip">Client IP address string.</param>
    /// <param name="maxRequests">Maximum number of requests allowed within the window.</param>
    /// <param name="window">Sliding time window.</param>
    bool IsAllowed(string ip, int maxRequests, TimeSpan window);
}
