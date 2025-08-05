using DotRush.Roslyn.CodeAnalysis.Extensions;

namespace DotRush.Roslyn.CodeAnalysis.Reflection;

public static class InternalBlockStructureOptions {
    internal static readonly Type? blockStructureOptionsType;

    public static readonly object? Default;

    static InternalBlockStructureOptions() {
        blockStructureOptionsType = ReflectionExtensions.GetTypeFromLoadedAssembly(KnownAssemblies.CommonFeaturesAssemblyName, "Microsoft.CodeAnalysis.Structure.BlockStructureOptions");
        Default = CreateNew();
    }

    public static object? CreateNew() {
        if (blockStructureOptionsType == null)
            return null;

        return Activator.CreateInstance(blockStructureOptionsType);
    }
}