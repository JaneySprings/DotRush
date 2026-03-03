using System.Reflection;
using Microsoft.CodeAnalysis.FindSymbols;

namespace DotRush.Roslyn.CodeAnalysis.Reflection;

public static class InternalReferenceLocation {
    internal static readonly Type? referenceLocationType;
    internal static readonly PropertyInfo? isWrittenToProperty;

    static InternalReferenceLocation() {
        referenceLocationType = typeof(ReferenceLocation);
        isWrittenToProperty = referenceLocationType?.GetProperty("IsWrittenTo", BindingFlags.Instance | BindingFlags.NonPublic);
    }

    public static bool IsWrittenTo(ReferenceLocation referenceLocation) {
        if (isWrittenToProperty == null)
            return false;

        if (isWrittenToProperty.GetValue(referenceLocation) is bool isWrittenTo)
            return isWrittenTo;

        return false;
    }
}