using System.Collections;
using System.Collections.Immutable;
using System.Reflection;
using DotRush.Roslyn.CodeAnalysis.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Host;
using Microsoft.CodeAnalysis.Text;

namespace DotRush.Roslyn.CodeAnalysis.Reflection;

public static class InternalCSharpInlineHintsService {
    internal static readonly Type? csharpCSharpInlineHintsServiceType;
    internal static readonly MethodInfo? getInlineHintsAsyncMethod;

    static InternalCSharpInlineHintsService() {
        csharpCSharpInlineHintsServiceType = ReflectionExtensions.GetTypeFromLoadedAssembly(KnownAssemblies.CSharpFeaturesAssemblyName, "Microsoft.CodeAnalysis.CSharp.InlineHints.CSharpInlineHintsService");
        getInlineHintsAsyncMethod = csharpCSharpInlineHintsServiceType?.GetMethod("GetInlineHintsAsync");
    }

    public static object? CreateNew() {
        if (csharpCSharpInlineHintsServiceType == null)
            return null;

        return Activator.CreateInstance(csharpCSharpInlineHintsServiceType);
    }
    public static async Task<IEnumerable?> GetInlineHintsAsync(object? inlineHintsService, Document document, TextSpan textSpan, object? inlineHintsOptions, bool displayAllOverride, CancellationToken cancellationToken) {
        if (inlineHintsService == null || getInlineHintsAsyncMethod == null || inlineHintsOptions == null)
            return null;

        var taskObject = getInlineHintsAsyncMethod.Invoke(inlineHintsService, new object?[] { document, textSpan, inlineHintsOptions, displayAllOverride, cancellationToken });
        if (taskObject is Task task)
            await task.ConfigureAwait(false);

        return (IEnumerable?)taskObject?.GetType().GetProperty("Result")?.GetValue(taskObject);
    }
}