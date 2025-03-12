using System.Reflection;
using DotRush.Common.Logging;

namespace DotRush.Roslyn.CodeAnalysis.Extensions;

public static class ReflectionExtensions {
    public static IEnumerable<TypeInfo>? LoadAssembly(string assemblyPathOrName) {
        Assembly? assembly;
        try {
            var assemblyName = GetAssemblyName(assemblyPathOrName);
            var loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies();
            assembly = loadedAssemblies.FirstOrDefault(a => a.GetName().Name == assemblyName);
            
            if (assembly != null) {
                CurrentSessionLogger.Debug($"Assembly '{assemblyName}' is already loaded, reusing existing instance.");
                return assembly.DefinedTypes;
            }
            
            if (File.Exists(assemblyPathOrName))
                assembly = Assembly.LoadFrom(assemblyPathOrName);
            else
                assembly = Assembly.Load(assemblyPathOrName);

            return assembly.DefinedTypes;
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
}