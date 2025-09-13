using DotRush.Roslyn.CodeAnalysis.Extensions;

namespace DotRush.Roslyn.CodeAnalysis.Reflection;

public static class InternalInlineHintsOptions {
    internal static readonly Type? inlineHintsOptionsType;

    public static readonly object? Default;

    static InternalInlineHintsOptions() {
        inlineHintsOptionsType = ReflectionExtensions.GetTypeFromLoadedAssembly(KnownAssemblies.CommonFeaturesAssemblyName, "Microsoft.CodeAnalysis.InlineHints.InlineHintsOptions");
        Default = CreateNew();
    }

    public static object? CreateNew() {
        if (inlineHintsOptionsType == null)
            return null;

        return Activator.CreateInstance(inlineHintsOptionsType);
    }
}