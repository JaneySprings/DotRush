namespace DotRush.Common.Collections;

public class NullableCollection<T> : List<T?> where T : class {
    public bool IsEmpty => Count == 0;

    public NullableCollection() { }
    public NullableCollection(IEnumerable<T?> collection) : base(collection) { }
    public NullableCollection(int capacity) : base(capacity) { }

    public List<T> ToList() {
        return this.Where(x => x != null).Select(x => x!).ToList();
    }
}