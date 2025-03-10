namespace DotRush.Roslyn.CodeAnalysis;

public class MemoryCache<TValue> where TValue : class {
    private readonly Dictionary<string, IEnumerable<string>> cache = new Dictionary<string, IEnumerable<string>>();
    private readonly Dictionary<string, TValue> componentTable = new Dictionary<string, TValue>();
    private readonly object lockObject = new object();

    internal IEnumerable<string> Keys => cache.Keys;
    internal int Count => componentTable.Count;
    internal bool ThrowOnCreation { get; set; }

    public IEnumerable<TValue> GetOrCreate(string key, Func<IEnumerable<TValue>> factory) {
        lock (lockObject) {
            if (cache.TryGetValue(key, out var value))
                return value.Select(x => componentTable[x]).ToArray();

            if (ThrowOnCreation)
                throw new InvalidOperationException($"Component {key} not found");

            var newValue = factory();
            foreach (var item in newValue)
                componentTable.TryAdd(GetValueId(item) , item);

            cache.Add(key, newValue.Select(GetValueId));
            return newValue;
        }
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