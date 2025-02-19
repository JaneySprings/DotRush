namespace DotRush.Roslyn.Common.Extensions;

public static class CollectionExtensions {
    public static void AddRange<T>(this ICollection<T> collection, IEnumerable<T> items) {
        foreach (var item in items)
            collection.Add(item);
    }
    public static void ForEach<T>(this IEnumerable<T> collection, Action<T> action) {
        foreach (var item in collection)
            action(item);
    }
}