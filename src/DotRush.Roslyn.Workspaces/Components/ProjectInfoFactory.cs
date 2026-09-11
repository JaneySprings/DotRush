using System.Collections.Immutable;
using System.Runtime.InteropServices;
using System.Text;
using DotRush.Common.Logging;
using DotRush.Roslyn.Workspaces.Extensions;
using DotRush.Roslyn.Workspaces.MSBuild;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DotRush.Roslyn.Workspaces.Components;

/// <summary>
/// Turns the compiler command line produced by a design-time build into a Roslyn <see cref="ProjectInfo"/>.
/// </summary>
internal sealed class ProjectInfoFactory {
    private readonly MetadataReferenceCache metadataReferences;
    private readonly IAnalyzerAssemblyLoader analyzerLoader;
    private readonly Dictionary<ProjectFileInfo, ProjectId> projectIds;
    private readonly Dictionary<string, ProjectId> projectIdsByOutputPath;

    public ProjectInfoFactory(MetadataReferenceCache metadataReferences, IAnalyzerAssemblyLoader analyzerLoader, ImmutableArray<ProjectFileInfo> fileInfos) {
        this.metadataReferences = metadataReferences;
        this.analyzerLoader = analyzerLoader;
        projectIds = fileInfos.ToDictionary(info => info, info => ProjectId.CreateNewId(info.ProjectName));
        projectIdsByOutputPath = new Dictionary<string, ProjectId>(StringComparer.OrdinalIgnoreCase);
        foreach (var info in fileInfos) {
            foreach (var outputPath in new[] { info.OutputFilePath, info.OutputRefFilePath }.OfType<string>())
                projectIdsByOutputPath.TryAdd(outputPath, projectIds[info]);
        }
    }

    public ProjectId GetProjectId(ProjectFileInfo fileInfo) => projectIds[fileInfo];

    public ProjectInfo Create(ProjectFileInfo fileInfo) {
        var projectId = projectIds[fileInfo];
        var projectDirectory = fileInfo.ProjectDirectory;
        var arguments = CSharpCommandLineParser.Default.Parse(fileInfo.CommandLineArgs, projectDirectory, RuntimeEnvironment.GetRuntimeDirectory());
        foreach (var error in arguments.Errors)
            CurrentSessionLogger.Error($"'{fileInfo.ProjectName}' command line: {error}");

        var (projectReferences, metadataReferences) = ResolveReferences(fileInfo, projectId, arguments);
        var compilationOptions = arguments.CompilationOptions
            .WithXmlReferenceResolver(new XmlFileResolver(projectDirectory))
            .WithSourceReferenceResolver(new SourceFileResolver(ImmutableArray<string>.Empty, projectDirectory))
            .WithStrongNameProvider(new DesktopStrongNameProvider(arguments.KeyFileSearchPaths))
            .WithAssemblyIdentityComparer(DesktopAssemblyIdentityComparer.Default);
        // Parse the documentation comments even when the project does not emit an XML file, they are shown on hover
        var parseOptions = arguments.ParseOptions.DocumentationMode == DocumentationMode.None
            ? arguments.ParseOptions.WithDocumentationMode(DocumentationMode.Parse)
            : arguments.ParseOptions;
        var assemblyName = string.IsNullOrEmpty(arguments.OutputFileName) ? fileInfo.Name : Path.GetFileNameWithoutExtension(arguments.OutputFileName);

        var projectInfo = ProjectInfo.Create(
                projectId,
                VersionStamp.Create(),
                fileInfo.ProjectName,
                assemblyName,
                LanguageNames.CSharp,
                fileInfo.FilePath,
                fileInfo.OutputFilePath,
                compilationOptions,
                parseOptions,
                CreateDocuments(projectId, projectDirectory, arguments.SourceFiles.Select(file => file.Path).Where(path => !Path.GetFileName(path).StartsWith("TemporaryGeneratedFile_", StringComparison.OrdinalIgnoreCase)), arguments.Encoding),
                projectReferences,
                metadataReferences,
                ResolveAnalyzerReferences(fileInfo, arguments),
                CreateDocuments(projectId, projectDirectory, arguments.AdditionalFiles.Select(file => file.Path), arguments.Encoding),
                isSubmission: false,
                hostObjectType: null,
                outputRefFilePath: fileInfo.OutputRefFilePath)
            .WithAnalyzerConfigDocuments(CreateDocuments(projectId, projectDirectory, arguments.AnalyzerConfigPaths, arguments.Encoding))
            .WithDefaultNamespace(fileInfo.DefaultNamespace);
        return projectInfo.WithCompilationOutputInfo(projectInfo.CompilationOutputInfo
            .WithAssemblyPath(fileInfo.OutputFilePath)
            .WithGeneratedFilesOutputDirectory(arguments.GeneratedFilesOutputDirectory));
    }

    private (List<ProjectReference>, List<MetadataReference>) ResolveReferences(ProjectFileInfo fileInfo, ProjectId projectId, CommandLineArguments arguments) {
        var projectReferences = new List<ProjectReference>();
        var resolvedReferences = new List<MetadataReference>();
        foreach (var reference in arguments.MetadataReferences) {
            var path = WorkspaceExtensions.GetFullPath(fileInfo.ProjectDirectory, reference.Reference)!;
            // The compiler references the output of a project reference, use the project itself when it is loaded
            if (projectIdsByOutputPath.TryGetValue(path, out var referencedProjectId) && referencedProjectId != projectId) {
                if (projectReferences.All(it => it.ProjectId != referencedProjectId))
                    projectReferences.Add(new ProjectReference(referencedProjectId, reference.Properties.Aliases, reference.Properties.EmbedInteropTypes));
            }
            else if (File.Exists(path))
                resolvedReferences.Add(metadataReferences.GetReference(path, reference.Properties));
            else
                CurrentSessionLogger.Debug($"'{fileInfo.ProjectName}': skipping missing reference '{path}'");
        }
        return (projectReferences, resolvedReferences);
    }
    private List<AnalyzerReference> ResolveAnalyzerReferences(ProjectFileInfo fileInfo, CommandLineArguments arguments) {
        var analyzerReferences = new List<AnalyzerReference>();
        foreach (var path in arguments.AnalyzerReferences.Select(it => WorkspaceExtensions.GetFullPath(fileInfo.ProjectDirectory, it.FilePath)!).Distinct(StringComparer.OrdinalIgnoreCase)) {
            if (!File.Exists(path)) {
                CurrentSessionLogger.Debug($"'{fileInfo.ProjectName}': skipping missing analyzer '{path}'");
                continue;
            }
            analyzerLoader.AddDependencyLocation(path);
            analyzerReferences.Add(new AnalyzerFileReference(path, analyzerLoader));
        }
        return analyzerReferences;
    }
    private static List<DocumentInfo> CreateDocuments(ProjectId projectId, string projectDirectory, IEnumerable<string> filePaths, Encoding? encoding) {
        var documents = new List<DocumentInfo>();
        foreach (var path in filePaths.Select(it => WorkspaceExtensions.GetFullPath(projectDirectory, it)!).Distinct(StringComparer.OrdinalIgnoreCase)) {
            if (!File.Exists(path)) {
                CurrentSessionLogger.Debug($"Skipping missing document '{path}'");
                continue;
            }
            var documentId = DocumentId.CreateNewId(projectId, path);
            var folders = ProjectExtensions.GetFolders(projectDirectory, path);
            documents.Add(DocumentInfo.Create(documentId, Path.GetFileName(path), folders, SourceCodeKind.Regular, new FileTextLoader(path, encoding), path));
        }
        return documents;
    }
}
