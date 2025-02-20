using System.Reflection;
using DotRush.Common.Logging;

namespace DotRush.Roslyn.CodeAnalysis.Extensions;

public static class ReflectionExtensions {
    public static IEnumerable<TypeInfo>? LoadAssembly(string assemblyPathOrName) {
        Assembly? assembly;
        try {
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
}