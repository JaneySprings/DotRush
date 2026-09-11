using System.Collections.Immutable;
using DotRush.Common.Logging;
using DotRush.Roslyn.Workspaces.Extensions;

namespace DotRush.Roslyn.Workspaces.MSBuild;

/// <summary>
/// Runs the same design-time build Roslyn uses (Compile without executing the compiler) through the MSBuild CLI
/// and collects the compiler command line, outputs and references for every target framework of a project.
/// </summary>
internal static class DesignTimeBuilder {
    private static readonly IReadOnlyDictionary<string, string> designTimeProperties = new Dictionary<string, string> {
        ["DesignTimeBuild"] = "true",
        ["BuildingProject"] = "false",
        ["BuildProjectReferences"] = "false",
        ["SkipCompilerExecution"] = "true",
        ["ProvideCommandLineArgs"] = "true",
        ["ContinueOnError"] = "ErrorAndContinue",
        ["ShouldUnsetParentConfigurationAndPlatform"] = "false",
        // Forces CoreCompile to run even when the outputs are up to date
        ["NonExistentFile"] = Path.Combine("__NonExistentSubDir__", "__NonExistentFile__"),
    };

    public static async Task<ImmutableArray<ProjectFileInfo>> BuildAsync(string projectPath, IReadOnlyDictionary<string, string> workspaceProperties, CancellationToken cancellationToken) {
        var evaluation = CreateRequest(projectPath, workspaceProperties, targetFramework: null, targets: null);
        evaluation.Properties.AddRange(new[] { "TargetFramework", "TargetFrameworks" });
        var evaluationResult = await MSBuildCli.RunAsync(evaluation, cancellationToken).ConfigureAwait(false);
        if (evaluationResult.IsEmpty)
            throw new InvalidOperationException($"Project '{projectPath}' could not be evaluated, see the log for MSBuild errors");

        var targetFramework = evaluationResult.GetProperty("TargetFramework");
        if (!string.IsNullOrWhiteSpace(targetFramework))
            return ImmutableArray.Create(await BuildAsync(projectPath, workspaceProperties, targetFramework, isMultiTargeted: false, cancellationToken).ConfigureAwait(false));

        // The outer build of an SDK project has no items, build every inner (per framework) one
        var targetFrameworks = evaluationResult.GetProperty("TargetFrameworks").Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct().ToArray();
        if (targetFrameworks.Length == 0)
            return ImmutableArray.Create(await BuildAsync(projectPath, workspaceProperties, null, isMultiTargeted: false, cancellationToken).ConfigureAwait(false));

        var results = ImmutableArray.CreateBuilder<ProjectFileInfo>(targetFrameworks.Length);
        foreach (var framework in targetFrameworks)
            results.Add(await BuildAsync(projectPath, workspaceProperties, framework, targetFrameworks.Length > 1, cancellationToken).ConfigureAwait(false));
        return results.MoveToImmutable();
    }

    /// <summary>
    /// The Compile and AdditionalFiles items of a loaded project as they are when the compiler runs. Items are adjusted by
    /// targets (e.g. the MAUI SDK drops the other platforms' folders before CoreCompile), so a plain evaluation is not enough.
    /// </summary>
    public static Task<MSBuildResult> GetCompilerItemsAsync(ProjectFileInfo fileInfo, IReadOnlyDictionary<string, string> workspaceProperties, CancellationToken cancellationToken) {
        var request = CreateRequest(fileInfo.FilePath, workspaceProperties, fileInfo.TargetFramework, "Compile;CoreCompile");
        request.Items.AddRange(new[] { "Compile", "AdditionalFiles" });
        return MSBuildCli.RunAsync(request, cancellationToken);
    }

    private static async Task<ProjectFileInfo> BuildAsync(string projectPath, IReadOnlyDictionary<string, string> workspaceProperties, string? targetFramework, bool isMultiTargeted, CancellationToken cancellationToken) {
        var request = CreateRequest(projectPath, workspaceProperties, targetFramework, "Compile;CoreCompile");
        request.Properties.AddRange(new[] { "TargetFramework", "TargetPath", "TargetRefPath", "RootNamespace", "BaseIntermediateOutputPath", "BaseOutputPath" });
        request.Items.AddRange(new[] { "CscCommandLineArgs", "ProjectReference" });

        var result = await MSBuildCli.RunAsync(request, cancellationToken).ConfigureAwait(false);
        var commandLineArgs = result.GetItems("CscCommandLineArgs").Select(item => item.Identity).ToImmutableArray();
        if (commandLineArgs.IsEmpty)
            CurrentSessionLogger.Error($"Design-time build of '{projectPath}' ({targetFramework}) produced no compiler arguments, check the MSBuild errors above");

        var projectDirectory = Path.GetDirectoryName(projectPath)!;
        return new ProjectFileInfo {
            FilePath = projectPath,
            ProjectDirectory = projectDirectory,
            TargetFramework = targetFramework ?? result.GetProperty("TargetFramework"),
            IsMultiTargeted = isMultiTargeted,
            OutputFilePath = WorkspaceExtensions.GetFullPath(projectDirectory, result.GetProperty("TargetPath")),
            OutputRefFilePath = WorkspaceExtensions.GetFullPath(projectDirectory, result.GetProperty("TargetRefPath")),
            IntermediateOutputPath = WorkspaceExtensions.GetFullPath(projectDirectory, result.GetProperty("BaseIntermediateOutputPath")) ?? Path.Combine(projectDirectory, "obj"),
            OutputPath = WorkspaceExtensions.GetFullPath(projectDirectory, result.GetProperty("BaseOutputPath")) ?? Path.Combine(projectDirectory, "bin"),
            DefaultNamespace = result.GetProperty("RootNamespace"),
            CommandLineArgs = commandLineArgs,
            ProjectReferences = result.GetItems("ProjectReference")
                .Where(item => !item.GetMetadata("ReferenceOutputAssembly").Equals("false", StringComparison.OrdinalIgnoreCase))
                .Select(item => WorkspaceExtensions.GetFullPath(projectDirectory, string.IsNullOrEmpty(item.FullPath) ? item.Identity : item.FullPath))
                .OfType<string>()
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToImmutableArray(),
        };
    }
    private static MSBuildRequest CreateRequest(string projectPath, IReadOnlyDictionary<string, string> workspaceProperties, string? targetFramework, string? targets) {
        var request = new MSBuildRequest { ProjectPath = projectPath, Targets = targets };
        if (targets != null) {
            foreach (var (name, value) in designTimeProperties)
                request.GlobalProperties[name] = value;
        }
        foreach (var (name, value) in workspaceProperties)
            request.GlobalProperties[name] = value;
        // Items of SDK projects are only defined in the inner (per framework) evaluation, even for a single <TargetFrameworks>
        if (!string.IsNullOrEmpty(targetFramework))
            request.GlobalProperties["TargetFramework"] = targetFramework;
        return request;
    }
}
