using System.Collections.Immutable;
using DotRush.Common.Extensions;

namespace DotRush.Roslyn.Workspaces.MSBuild;

/// <summary>
/// Result of a design-time build of a single project for a single target framework.
/// </summary>
internal sealed class ProjectFileInfo {
    public required string FilePath { get; init; }
    public required string ProjectDirectory { get; init; }
    public required string TargetFramework { get; init; }
    public required bool IsMultiTargeted { get; init; }
    public string? OutputFilePath { get; init; }
    public string? OutputRefFilePath { get; init; }
    public required string IntermediateOutputPath { get; init; }
    public required string OutputPath { get; init; }
    public string? DefaultNamespace { get; init; }
    public required ImmutableArray<string> CommandLineArgs { get; init; }
    public required ImmutableArray<string> ProjectReferences { get; init; }

    public string Name => Path.GetFileNameWithoutExtension(FilePath);
    public string ProjectName => IsMultiTargeted ? $"{Name}({TargetFramework})" : Name;

    /// <summary>
    /// Whether the file could be an item of this project: inside the project directory and not a build output.
    /// MSBuild decides the rest (see <see cref="WorkspaceHost.CreateDocuments"/>).
    /// </summary>
    public bool CanContainDocument(string filePath) {
        return PathExtensions.StartsWith(filePath, ProjectDirectory + Path.DirectorySeparatorChar) && !IsBuildOutput(filePath);
    }
    public bool IsBuildOutput(string filePath) {
        return PathExtensions.StartsWith(filePath, IntermediateOutputPath) || PathExtensions.StartsWith(filePath, OutputPath);
    }
}
