using System.Collections;
using System.Reflection;
using DotRush.Roslyn.CodeAnalysis.Extensions;
using Microsoft.CodeAnalysis.Text;

namespace DotRush.Roslyn.CodeAnalysis.Reflection;

public static class InternalBlockStructure {
    internal static readonly Type? blockStructureType;
    internal static readonly Type? blockSpanType;
    internal static readonly PropertyInfo? spansProperty;
    internal static readonly PropertyInfo? textSpanProperty;
    internal static readonly PropertyInfo? bannerTextProperty;

    static InternalBlockStructure() {
        blockStructureType = ReflectionExtensions.GetTypeFromLoadedAssembly(KnownAssemblies.CommonFeaturesAssemblyName, "Microsoft.CodeAnalysis.Structure.BlockStructure");
        blockSpanType = ReflectionExtensions.GetTypeFromLoadedAssembly(KnownAssemblies.CommonFeaturesAssemblyName, "Microsoft.CodeAnalysis.Structure.BlockSpan");
        spansProperty = blockStructureType?.GetProperty("Spans");
        textSpanProperty = blockSpanType?.GetProperty("TextSpan");
        bannerTextProperty = blockSpanType?.GetProperty("BannerText");
    }

    public static IEnumerable? GetSpans(object? blockStructure) {
        if (spansProperty == null || blockStructure == null)
            return null;

        return spansProperty.GetValue(blockStructure) as IEnumerable;
    }
    public static TextSpan GetTextSpan(object? blockSpan) {
        var result = textSpanProperty?.GetValue(blockSpan);
        if (result is TextSpan textSpan)
            return textSpan;

        return default(TextSpan);
    }
    public static string? GetBannerText(object? blockSpan) {
        if (bannerTextProperty == null || blockSpan == null)
            return null;

        return bannerTextProperty.GetValue(blockSpan) as string;
    }
}