using System.Reflection;
using DotRush.Roslyn.CodeAnalysis.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Host;

namespace DotRush.Roslyn.CodeAnalysis.Reflection;

public static class InternalCSharpBlockStructureService {
    internal static readonly Type? csharpBlockStructureServiceType;
    internal static readonly MethodInfo? getBlockStructureAsyncMethod;

    static InternalCSharpBlockStructureService() {
        csharpBlockStructureServiceType = ReflectionExtensions.GetTypeFromLoadedAssembly(KnownAssemblies.CSharpFeaturesAssemblyName, "Microsoft.CodeAnalysis.CSharp.Structure.CSharpBlockStructureService");
        getBlockStructureAsyncMethod = csharpBlockStructureServiceType?.GetMethod("GetBlockStructureAsync");
    }

    public static object? CreateNew(SolutionServices solutionServices) {
        if (csharpBlockStructureServiceType == null)
            return null;

        return Activator.CreateInstance(csharpBlockStructureServiceType, solutionServices);
    }
    public static async Task<object?> GetBlockStructureAsync(object? blockStructureService, Document document, object? blockStructureOptions, CancellationToken cancellationToken) {
        if (blockStructureService == null || getBlockStructureAsyncMethod == null || blockStructureOptions == null)
            return null;

        var taskObject = getBlockStructureAsyncMethod.Invoke(blockStructureService, new object?[] { document, blockStructureOptions, cancellationToken });
        if (taskObject is Task task)
            await task.ConfigureAwait(false);

        return taskObject?.GetType().GetProperty("Result")?.GetValue(taskObject);
    }
}