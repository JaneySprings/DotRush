using System.Reflection;
using DotRush.Common.Extensions;
using Microsoft.CodeAnalysis;

namespace DotRush.Roslyn.Workspaces.Components;

/// <summary>
/// Loads analyzer assemblies directly from their original location. Used where assemblies are not locked
/// by the loading process (non-Windows), <see cref="ShadowCopyAnalyzerLoader"/> is used otherwise.
/// </summary>
internal sealed class DirectAnalyzerLoader : IAnalyzerAssemblyLoader {
    public void AddDependencyLocation(string fullPath) { }

    public Assembly LoadFromPath(string fullPath) {
        return ReflectionExtensions.LoadAssembly(fullPath) ?? throw new FileLoadException($"Unable to load analyzer assembly from '{fullPath}'.");
    }
}
