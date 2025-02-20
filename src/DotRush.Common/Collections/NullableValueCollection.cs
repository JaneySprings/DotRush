namespace DotRush.Common.Collections;

public class NullableValueCollection<T> : List<T?> where T : struct {
    public bool IsEmpty => Count == 0;

    public NullableValueCollection() { }
    public NullableValueCollection(IEnumerable<T?> collection) : base(collection) { }
    public NullableValueCollection(int capacity) : base(capacity) { }

    public List<T> ToNonNullableList() {
        return this.Where(x => x != null).Select(x => x!.Value).ToList();
    }
}