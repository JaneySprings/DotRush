using DotRush.Roslyn.Common.Logging;

namespace DotRush.Roslyn.CodeAnalysis;

public class MemoryCache<TValue> where TValue : class {
    private readonly Dictionary<string, IEnumerable<TValue>> cache = new Dictionary<string, IEnumerable<TValue>>();

    public IEnumerable<TValue> GetOrCreate(string key, Func<IEnumerable<TValue>> factory) {
        if (cache.TryGetValue(key, out var value))
            return value;

        var newValue = factory();
        cache.Add(key, newValue);
        return newValue;
    }
    public void Clear() {
        cache.Clear();
    }
    public bool Clear(string key) {
        return cache.Remove(key);
    }
}