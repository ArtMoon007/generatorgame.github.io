using System.Collections.Concurrent;

namespace GeneratorGame.Services;

public class OnlineTracker
{
    private static readonly TimeSpan ActiveWindow = TimeSpan.FromSeconds(30);
    private readonly ConcurrentDictionary<string, DateTimeOffset> _seen = new();
    private readonly ConcurrentDictionary<string, byte> _visitors = new();

    public OnlineSnapshot Touch(string clientId)
    {
        var now = DateTimeOffset.UtcNow;
        _seen[clientId] = now;
        _visitors.TryAdd(clientId, 0);
        Cleanup(now);
        return Snapshot(now);
    }

    public int Count(DateTimeOffset? at = null)
    {
        var now = at ?? DateTimeOffset.UtcNow;
        Cleanup(now);
        return _seen.Count(kv => now - kv.Value <= ActiveWindow);
    }

    public OnlineSnapshot Snapshot(DateTimeOffset? at = null)
    {
        var now = at ?? DateTimeOffset.UtcNow;
        Cleanup(now);
        return new OnlineSnapshot(
            _seen.Count(kv => now - kv.Value <= ActiveWindow),
            _visitors.Count);
    }

    private void Cleanup(DateTimeOffset now)
    {
        foreach (var item in _seen)
        {
            if (now - item.Value > ActiveWindow)
            {
                _seen.TryRemove(item.Key, out _);
            }
        }
    }
}

public record OnlineSnapshot(int Online, int Visits);
