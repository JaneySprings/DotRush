namespace DotRush.Common.Extensions;

public static class CollectionExtensions {
    public static ICollection<T> AddRange<T>(this ICollection<T> collection, IEnumerable<T> items) {
        foreach (var item in items)
            collection.Add(item);

        return collection;
    }
    public static List<T> AddRanges<T>(this List<T> collection, params IEnumerable<T>[] sources) {
        foreach (var source in sources)
            collection.AddRange(source);

        return collection;
    }
    public static IEnumerable<T> ForEach<T>(this IEnumerable<T> collection, Action<T> action) {
        foreach (var item in collection)
            action(item);

        return collection;
    }
    public static IEnumerable<T> WhereNotNull<T>(this IEnumerable<T?> collection) where T : class {
        return collection.Where(item => item != null)!;
    }
}