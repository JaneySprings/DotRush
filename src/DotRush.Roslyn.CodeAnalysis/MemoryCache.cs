using DotRush.Roslyn.Common.Logging;

namespace DotRush.Roslyn.CodeAnalysis;

public class MemoryCache<TValue> where TValue : class {
    private readonly Dictionary<string, IEnumerable<string>> cache = new Dictionary<string, IEnumerable<string>>();
    private readonly Dictionary<string, TValue> componentTable = new Dictionary<string, TValue>();

    public IEnumerable<TValue> GetOrCreate(string key, Func<IEnumerable<TValue>> factory) {
        if (cache.TryGetValue(key, out var value))
            return value.Select(x => componentTable[x]);

        var newValue = factory();
        foreach (var item in newValue)
            componentTable.TryAdd(GetValueId(item) , item);

        cache.Add(key, newValue.Select(GetValueId));
        return newValue;
    }
    public void Clear() {
        cache.Clear();
    }
    public bool Clear(string key) {
        return cache.Remove(key);
    }

    private string GetValueId(TValue value) {
        return value.ToString() ?? value.GetType().ToString();
    }
}