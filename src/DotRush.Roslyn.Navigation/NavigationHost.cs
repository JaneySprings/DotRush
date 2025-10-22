using DotRush.Common.Extensions;
using DotRush.Common.Logging;
using DotRush.Roslyn.Navigation.Decompilation;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using FileSystemExtensions = DotRush.Common.Extensions.FileSystemExtensions;

namespace DotRush.Roslyn.Navigation;

public class NavigationHost {
    public string DecompiledCodeDirectory { get; }
    public string GeneratedCodeDirectory { get; }
    public Solution? Solution { get; private set; }

    private readonly CurrentClassLogger currentClassLogger;
    private readonly AssemblyDecompiler assemblyDecompiler;
    private readonly Dictionary<string, ProjectId> decompilerDocuments;

    public NavigationHost() {
        var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
        DecompiledCodeDirectory = Path.Combine(baseDirectory, "_decompiled_");
        GeneratedCodeDirectory = Path.Combine(baseDirectory, "_generated_");
        currentClassLogger = new CurrentClassLogger(nameof(NavigationHost));
        decompilerDocuments = new Dictionary<string, ProjectId>();
        assemblyDecompiler = new AssemblyDecompiler();
    }

    public async Task<string?> EmitDecompiledFileAsync(ISymbol symbol, Project project, CancellationToken cancellationToken) {
        var csharpDecompiler = await assemblyDecompiler.CreateDecompilerAsync(symbol.ContainingAssembly, project, cancellationToken).ConfigureAwait(false);
        if (csharpDecompiler == null) {
            currentClassLogger.Debug($"Failed to collect metadata for assembly '{symbol?.ContainingAssembly?.Name}'");
            return null;
        }

        var syntaxTree = assemblyDecompiler.DecompileType(csharpDecompiler, symbol);
        var outputFilePath = Path.Combine(DecompiledCodeDirectory, project.Name, symbol.ContainingAssembly.Name, syntaxTree.FileName);
        FileSystemExtensions.WriteAllText(outputFilePath, syntaxTree.ToString());
        // FileSystemExtensions.MakeFileReadOnly(outputFilePath); // TODO: May be issues with deleting files if they are read-only
        CreateDocument(outputFilePath, project.Id);

        currentClassLogger.Debug($"Emit decompiled file: {outputFilePath}");
        return outputFilePath;
    }
    public async Task<string?> EmitCompilerGeneratedFileAsync(Location location, Project project, CancellationToken cancellationToken) {
        var documentPath = location.SourceTree?.FilePath;
        if (location.SourceTree == null || documentPath == null)
            return null;

        var outputFilePath = Path.Combine(GeneratedCodeDirectory, project.Name, documentPath);
        var sourceText = await location.SourceTree.GetTextAsync(cancellationToken).ConfigureAwait(false);
        FileSystemExtensions.WriteAllText(outputFilePath, sourceText.ToString());
        // FileSystemExtensions.MakeFileReadOnly(outputFilePath); // TODO: May be issues with deleting files if they are read-only
        CreateDocument(outputFilePath, project.Id);

        currentClassLogger.Debug($"Emit source generated file: {outputFilePath}");
        return outputFilePath;
    }
    public void UpdateSolution(Solution? solution) {
        var currentSolution = solution;
        foreach (var pair in decompilerDocuments) {
            var project = currentSolution?.GetProject(pair.Value);
            if (project == null || project.Documents.Any(d => PathExtensions.Equals(d.FilePath, pair.Key)))
                continue;

            var documentName = Path.GetFileName(pair.Key);
            var sourceText = SourceText.From(FileSystemExtensions.TryReadText(pair.Key, string.Empty));
            var document = project.AddDocument(documentName, sourceText, filePath: pair.Key);
            currentSolution = document.Project.Solution;
            currentClassLogger.Debug($"Document {document.Name} has been added to {project.Name}");
        }

        Solution = currentSolution;
    }
    public void CloseDocument(string documentPath) {
        if (decompilerDocuments.Remove(documentPath))
            currentClassLogger.Debug($"Document {documentPath} has been removed form {nameof(NavigationHost)} cache");
    }

    private void CreateDocument(string documentPath, ProjectId projectId) {
        decompilerDocuments.TryAdd(documentPath, projectId);
        UpdateSolution(Solution);
    }
}