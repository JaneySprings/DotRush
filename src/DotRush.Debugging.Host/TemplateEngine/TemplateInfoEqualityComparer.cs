using Microsoft.TemplateEngine.Abstractions;

namespace DotRush.Debugging.Host.TemplateEngine;

class TemplateInfoEqualityComparer : IEqualityComparer<ITemplateInfo> {
    public static TemplateInfoEqualityComparer Default { get; } = new TemplateInfoEqualityComparer();

    public bool Equals(ITemplateInfo? x, ITemplateInfo? y) {
        if (x == null && y == null)
            return true;
        if (x == null || y == null)
            return false;

        return GetHashCode(x) == GetHashCode(y);
    }
    public int GetHashCode(ITemplateInfo obj) {
        var hashCode = HashCode.Combine(obj.Name, obj.Author);
        foreach (var shortName in obj.ShortNameList)
            hashCode = HashCode.Combine(hashCode, shortName);

        return hashCode;
    }
}