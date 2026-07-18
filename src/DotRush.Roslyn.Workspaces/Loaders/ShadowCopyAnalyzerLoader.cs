using System.Collections.Concurrent;
using System.Reflection;
using DotRush.Common.Extensions;
using Microsoft.CodeAnalysis;
using FileSystemExtensions = DotRush.Common.Extensions.FileSystemExtensions;

namespace DotRush.Roslyn.Workspaces.Loaders;

public sealed class ShadowCopyAnalyzerLoader : IAnalyzerAssemblyLoader {
    private readonly string shadowCopyDirectory;
    private readonly ConcurrentDictionary<string, string> dependencyPathsByName;
    private readonly object copyLock = new();

    public ShadowCopyAnalyzerLoader() {
        var baseDirectory = Path.Combine(AppContext.BaseDirectory, "_analyzersCopy_");
        shadowCopyDirectory = Path.Combine(baseDirectory, Guid.NewGuid().ToString());
        dependencyPathsByName = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (!Directory.Exists(baseDirectory))
            Directory.CreateDirectory(baseDirectory);
        foreach (var directory in Directory.EnumerateDirectories(baseDirectory))
            FileSystemExtensions.TryDeleteDirectory(directory);

        Directory.CreateDirectory(shadowCopyDirectory);
        AppDomain.CurrentDomain.AssemblyResolve += ResolveDependency;
    }

    public void AddDependencyLocation(string fullPath) {
        // Remember where each analyzer/dependency assembly originally lives so a private dependency that
        // isn't loaded directly (only referenced by another analyzer) can still be shadow-copied on demand.
        dependencyPathsByName[ReflectionExtensions.GetAssemblyName(fullPath)] = fullPath;
    }
    public Assembly LoadFromPath(string fullPath) {
        AddDependencyLocation(fullPath);

        var assembly = ReflectionExtensions.FindLoadedAssembly(ReflectionExtensions.GetAssemblyName(fullPath));
        if (assembly != null)
            return assembly;

        var shadowPath = CreateShadowCopy(fullPath);
        return ReflectionExtensions.LoadAssembly(shadowPath) ?? throw new FileLoadException($"Unable to load analyzer assembly from '{shadowPath}'.");
    }

    private Assembly? ResolveDependency(object? sender, ResolveEventArgs args) {
        var assemblyName = new AssemblyName(args.Name).Name;
        if (string.IsNullOrEmpty(assemblyName) || !dependencyPathsByName.TryGetValue(assemblyName, out var originalPath))
            return null;

        return LoadFromPath(originalPath);
    }
    private string CreateShadowCopy(string originalPath) {
        // Copy each analyzer into a per-source-directory shadow folder. This keeps assemblies from the same
        // package together (so co-located dependencies resolve) and avoids collisions between packages that
        // ship files with identical names.
        var originalDirectory = Path.GetDirectoryName(originalPath) ?? shadowCopyDirectory;
        var targetDirectory = Path.Combine(shadowCopyDirectory, originalDirectory.GetHashCode(StringComparison.OrdinalIgnoreCase).ToString("x"));
        if (!Directory.Exists(targetDirectory))
            Directory.CreateDirectory(targetDirectory);

        var targetPath = Path.Combine(targetDirectory, Path.GetFileName(originalPath));
        if (File.Exists(targetPath))
            return targetPath;

        lock (copyLock) {
            if (File.Exists(targetPath))
                return targetPath;
            FileSystemExtensions.TryCopyFile(originalPath, targetPath, overwrite: true);
        }
        return targetPath;
    }
}
