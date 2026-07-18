using System.Reflection;
using DotRush.Common.Logging;

namespace DotRush.Common.Extensions;

public static class ReflectionExtensions {
    public static Assembly? LoadAssembly(string assemblyPathOrName) {
        try {
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
        catch (Exception ex) {
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
            assembly = LoadAssembly(assemblyName);

        return assembly?.GetType(typeName);
    }
    public static Assembly? FindLoadedAssembly(string assemblyName) {
        return AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == assemblyName);
    }
}