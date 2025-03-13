using System.Reflection;
using DotRush.Common.Logging;

namespace DotRush.Roslyn.CodeAnalysis.Extensions;

public static class ReflectionExtensions {
    public static IEnumerable<TypeInfo>? LoadAssembly(string assemblyPathOrName) {
        try {
            var assembly = LoadAssemblyCore(assemblyPathOrName);
            return assembly?.DefinedTypes;
        } catch (Exception ex) {
            CurrentSessionLogger.Error($"Loading assembly '{assemblyPathOrName}' failed, error: {ex.Message}");
            return null;
        }
    }
    public static string GetAssemblyName(string assemblyPathOrName) {
        if (assemblyPathOrName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            return Path.GetFileNameWithoutExtension(assemblyPathOrName);

        return Path.GetFileName(assemblyPathOrName);
    }
    public static Type? GetTypeFromLoadedAssembly(string assemblyName, string typeName) {
        var assembly = FindLoadedAssembly(assemblyName);
        if (assembly == null)
            assembly = LoadAssemblyCore(assemblyName);

        return assembly?.GetType(typeName);
    }

    private static Assembly? LoadAssemblyCore(string assemblyPathOrName) {
        var assemblyName = GetAssemblyName(assemblyPathOrName);
        var assembly = FindLoadedAssembly(assemblyName);

        if (assembly != null) {
            CurrentSessionLogger.Debug($"[Reflector]: Assembly '{assemblyName}' is already loaded, reusing existing instance.");
            return assembly;
        }

        if (File.Exists(assemblyPathOrName))
            assembly = Assembly.LoadFrom(assemblyPathOrName);
        else
            assembly = Assembly.Load(assemblyPathOrName);

        CurrentSessionLogger.Debug($"[Reflector]: Assembly '{assemblyName}' loaded.");
        return assembly;
    }
    private static Assembly? FindLoadedAssembly(string assemblyName) {
        return AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == assemblyName);
    }
}