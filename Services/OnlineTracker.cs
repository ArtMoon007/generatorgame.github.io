using System.Collections.Concurrent;

namespace GeneratorGame.Services;

public class OnlineTracker
{
    private static readonly TimeSpan ActiveWindow = TimeSpan.FromSeconds(30);
    private readonly ConcurrentDictionary<string, DateTimeOffset> _seen = new();

    public int Touch(string clientId)
    {
        var now = DateTimeOffset.UtcNow;
        _seen[clientId] = now;
        Cleanup(now);
        return Count(now);
    }

    public int Count(DateTimeOffset? at = null)
    {
        var now = at ?? DateTimeOffset.UtcNow;
        Cleanup(now);
        return _seen.Count(kv => now - kv.Value <= ActiveWindow);
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
