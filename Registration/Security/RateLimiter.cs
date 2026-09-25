using System.Collections.Concurrent;
using System.Net;

namespace Registration.Security;

/// <summary>
/// Ferestre glisante in memorie: cate evenimente a avut o cheie (IP, retea, global) in
/// ultima perioada. Se pierd la repornirea serverului, ceea ce e acceptabil: limitele
/// opresc avalansele, nu tin evidenta pe termen lung.
/// </summary>
public sealed class RateLimiter
{
    private readonly ConcurrentDictionary<string, Queue<DateTimeOffset>> _events = new();

    public int Count(string key, TimeSpan window, DateTimeOffset now)
    {
        if (!_events.TryGetValue(key, out var queue))
        {
            return 0;
        }

        lock (queue)
        {
            Trim(queue, now - window);
            return queue.Count;
        }
    }

    /// <summary>True daca a mai ramas loc sub limita (0 = fara limita).</summary>
    public bool Allows(string key, int limit, TimeSpan window, DateTimeOffset now) =>
        limit <= 0 || Count(key, window, now) < limit;

    public void Record(string key, DateTimeOffset now)
    {
        var queue = _events.GetOrAdd(key, _ => new Queue<DateTimeOffset>());
        lock (queue)
        {
            queue.Enqueue(now);
            while (queue.Count > 10_000)
            {
                queue.Dequeue();
            }
        }
    }

    /// <summary>Cat mai trebuie asteptat pana se elibereaza un loc.</summary>
    public TimeSpan RetryAfter(string key, TimeSpan window, DateTimeOffset now)
    {
        if (!_events.TryGetValue(key, out var queue))
        {
            return TimeSpan.Zero;
        }

        lock (queue)
        {
            Trim(queue, now - window);
            return queue.Count == 0 ? TimeSpan.Zero : queue.Peek() + window - now;
        }
    }

    /// <summary>Sterge cheile fara evenimente recente, ca memoria sa nu creasca la nesfarsit.</summary>
    public void Cleanup(TimeSpan maxWindow, DateTimeOffset now)
    {
        foreach (var pair in _events)
        {
            lock (pair.Value)
            {
                Trim(pair.Value, now - maxWindow);
                if (pair.Value.Count == 0)
                {
                    _events.TryRemove(pair.Key, out _);
                }
            }
        }
    }

    private static void Trim(Queue<DateTimeOffset> queue, DateTimeOffset since)
    {
        while (queue.Count > 0 && queue.Peek() < since)
        {
            queue.Dequeue();
        }
    }

    public static string IpKey(IPAddress? ip) => "ip:" + (Normalize(ip)?.ToString() ?? "-");

    /// <summary>Reteaua: /24 pentru IPv4, /64 pentru IPv6 (un abonat primeste de obicei un /64 intreg).</summary>
    public static string SubnetKey(IPAddress? ip)
    {
        ip = Normalize(ip);
        if (ip == null)
        {
            return "net:-";
        }

        var bytes = ip.GetAddressBytes();
        var keep = bytes.Length == 4 ? 3 : 8;
        for (var i = keep; i < bytes.Length; i++)
        {
            bytes[i] = 0;
        }

        return "net:" + new IPAddress(bytes) + (bytes.Length == 4 ? "/24" : "/64");
    }

    public static IPAddress? Normalize(IPAddress? ip) => ip != null && ip.IsIPv4MappedToIPv6 ? ip.MapToIPv4() : ip;
}
