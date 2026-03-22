using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using DotRush.Common.Logging;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using PathExtensions = DotRush.Common.Extensions.PathExtensions;

namespace DotRush.Roslyn.Workspaces.Extensions;

public static class AnalyzerShadowCopyExtensions {
    public static Project WithShadowCopiedAnalyzerReferences(this Project project) {
        var analyzerReferences = CreateShadowCopiedAnalyzerReferences(project.AnalyzerReferences);
        return analyzerReferences == null ? project : project.WithAnalyzerReferences(analyzerReferences);
    }
    public static Solution WithShadowCopiedAnalyzerReferences(this Solution solution) {
        var currentSolution = solution;

        foreach (var project in solution.Projects.ToArray()) {
            var analyzerReferences = CreateShadowCopiedAnalyzerReferences(project.AnalyzerReferences);
            if (analyzerReferences == null)
                continue;

            currentSolution = currentSolution.WithProjectAnalyzerReferences(project.Id, analyzerReferences);
        }

        return currentSolution;
    }

    private static List<AnalyzerReference>? CreateShadowCopiedAnalyzerReferences(IEnumerable<AnalyzerReference> analyzerReferences) {
        var result = new List<AnalyzerReference>();
        var hasChanges = false;

        foreach (var reference in analyzerReferences) {
            if (reference is not AnalyzerFileReference fileReference || string.IsNullOrEmpty(fileReference.FullPath) || !File.Exists(fileReference.FullPath)) {
                result.Add(reference);
                continue;
            }

            var shadowCopyPath = ShadowCopyDirectory.GetOrCreate(fileReference.FullPath);
            if (PathExtensions.Equals(shadowCopyPath, fileReference.FullPath)) {
                result.Add(reference);
                continue;
            }

            result.Add(new AnalyzerFileReference(shadowCopyPath, ShadowCopyAnalyzerAssemblyLoader.Instance));
            hasChanges = true;
        }

        return hasChanges ? result : null;
    }
}

internal sealed class ShadowCopyAnalyzerAssemblyLoader : IAnalyzerAssemblyLoader {
    private readonly Dictionary<string, string> dependencyLocations = new(StringComparer.OrdinalIgnoreCase);

    public static ShadowCopyAnalyzerAssemblyLoader Instance { get; } = new ShadowCopyAnalyzerAssemblyLoader();

    private ShadowCopyAnalyzerAssemblyLoader() { }

    public void AddDependencyLocation(string fullPath) {
        if (string.IsNullOrEmpty(fullPath))
            return;

        lock (dependencyLocations)
            dependencyLocations[Path.GetFileName(fullPath)] = fullPath;
    }
    public Assembly LoadFromPath(string fullPath) {
        if (string.IsNullOrEmpty(fullPath))
            throw new ArgumentException("Assembly path is null or empty", nameof(fullPath));

        if (!File.Exists(fullPath)) {
            lock (dependencyLocations) {
                if (!dependencyLocations.TryGetValue(Path.GetFileName(fullPath), out var dependencyPath))
                    throw new FileNotFoundException($"Analyzer assembly '{fullPath}' does not exist.", fullPath);

                fullPath = dependencyPath;
            }
        }

        return Assembly.LoadFrom(ShadowCopyDirectory.GetOrCreate(fullPath));
    }
}

internal static class ShadowCopyDirectory {
    private static readonly object SyncRoot = new();
    private static readonly string RootPath = Path.Combine(Path.GetTempPath(), "DotRush", "analyzers");

    public static string GetOrCreate(string assemblyPath) {
        if (string.IsNullOrEmpty(assemblyPath) || !File.Exists(assemblyPath))
            return assemblyPath;

        var fullAssemblyPath = Path.GetFullPath(assemblyPath);
        if (PathExtensions.StartsWith(fullAssemblyPath, RootPath))
            return fullAssemblyPath;

        var sourceDirectory = Path.GetDirectoryName(fullAssemblyPath);
        if (string.IsNullOrEmpty(sourceDirectory))
            return fullAssemblyPath;

        var shadowDirectory = Path.Combine(RootPath, GetDirectoryFingerprint(sourceDirectory));
        var shadowAssemblyPath = Path.Combine(shadowDirectory, Path.GetFileName(fullAssemblyPath));

        lock (SyncRoot) {
            if (!File.Exists(shadowAssemblyPath)) {
                Directory.CreateDirectory(shadowDirectory);

                foreach (var file in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.TopDirectoryOnly)) {
                    var shadowFilePath = Path.Combine(shadowDirectory, Path.GetFileName(file));
                    File.Copy(file, shadowFilePath, true);
                }

                CurrentSessionLogger.Debug($"[Reflector]: Shadow copied analyzer '{fullAssemblyPath}' to '{shadowAssemblyPath}'.");
            }
        }

        return shadowAssemblyPath;
    }

    private static string GetDirectoryFingerprint(string sourceDirectory) {
        var latestWriteTime = Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.TopDirectoryOnly)
            .Select(path => File.GetLastWriteTimeUtc(path).Ticks)
            .DefaultIfEmpty(0)
            .Max();
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{sourceDirectory}|{latestWriteTime}"));
        return Convert.ToHexString(bytes);
    }
}