using DotRush.Common.Extensions;
using DotRush.Common.Logging;
using DotRush.Roslyn.CodeAnalysis.Components;
using DotRush.Roslyn.CodeAnalysis.Reflection;
using DotRush.Roslyn.Navigation.Decompilation;
using DotRush.Roslyn.Navigation.Extensions;
using DotRush.Roslyn.Workspaces.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Text;
using FileSystemExtensions = DotRush.Common.Extensions.FileSystemExtensions;

namespace DotRush.Roslyn.Navigation;

public class NavigationHost : IClearable {
    public Solution? Solution { get; private set; }

    private readonly string decompiledCodeDirectory;
    private readonly string generatedCodeDirectory;
    private readonly CurrentClassLogger currentClassLogger;
    private readonly AssemblyDecompiler assemblyDecompiler;
    private readonly Dictionary<string, ProjectId> temporaryDocuments;

    public NavigationHost() {
        decompiledCodeDirectory = Path.Combine(AppContext.BaseDirectory, "_decompiled_");
        generatedCodeDirectory = Path.Combine(AppContext.BaseDirectory, "_generated_");
        currentClassLogger = new CurrentClassLogger(nameof(NavigationHost));
        temporaryDocuments = new Dictionary<string, ProjectId>();
        assemblyDecompiler = new AssemblyDecompiler();
    }

    public async Task<List<FileLinePositionSpan>> FindDefinitionsAsync(ISymbol symbol, Project project, CancellationToken cancellationToken) {
        var result = new List<FileLinePositionSpan>();
        foreach (var location in symbol.Locations) {
            if (location.IsInMetadata) {
                var decompiledSpan = await EmitDecompiledFileAsync(symbol, project, cancellationToken);
                if (decompiledSpan != null)
                    result.Add(decompiledSpan.Value);
                continue;
            }
            if (!location.IsInSource || location.SourceTree == null)
                continue;

            if (!File.Exists(location.SourceTree.FilePath)) {
                var generatedSpan = await EmitCompilerGeneratedFileAsync(location, project, cancellationToken);
                if (generatedSpan != null)
                    result.Add(generatedSpan.Value);
                continue;
            }

            result.Add(location.GetLineSpan());
        }

        return result;
    }
    public async Task<List<FileLinePositionSpan>> FindReferencesAsync(ISymbol symbol, CancellationToken cancellationToken) {
        var result = new List<FileLinePositionSpan>();
        if (Solution == null)
            return result;

        var referencedSymbols = await SymbolFinder.FindReferencesAsync(symbol, Solution, cancellationToken);
        var referenceLocations = referencedSymbols.SelectMany(it => it.Locations);
        if (symbol is IMethodSymbol methodSymbol) {
            if (methodSymbol.MethodKind == MethodKind.PropertyGet)
                referenceLocations = referenceLocations.Where(it => !InternalReferenceLocation.IsWrittenTo(it));
            if (methodSymbol.MethodKind == MethodKind.PropertySet)
                referenceLocations = referenceLocations.Where(it => InternalReferenceLocation.IsWrittenTo(it));
        }

        foreach (var referenceLocation in referenceLocations) {
            var location = referenceLocation.Location;
            if (!location.IsInSource || location.SourceTree == null)
                continue;

            if (!File.Exists(location.SourceTree.FilePath)) {
                var generatedSpan = await EmitCompilerGeneratedFileAsync(location, referenceLocation.Document.Project, cancellationToken);
                if (generatedSpan != null)
                    result.Add(generatedSpan.Value);
                continue;
            }

            result.Add(location.GetLineSpan());
        }

        return result;
    }
    private async Task<FileLinePositionSpan?> FindSymbolFileSpanAsync(ISymbol symbol, string filePath, ProjectId projectId, CancellationToken cancellationToken) {
        var fallbackSpan = new FileLinePositionSpan(filePath, default(LinePositionSpan));
        var project = Solution?.GetProject(projectId);
        var documentId = project?.GetDocumentIdsWithFilePath(filePath)?.FirstOrDefault();
        if (project == null || documentId == null)
            return fallbackSpan;

        var declarationId = SafeExtensions.Invoke(() => DocumentationCommentId.CreateDeclarationId(symbol.OriginalDefinition));
        var compilation = await project.GetCompilationAsync(cancellationToken);
        if (declarationId == null || compilation == null)
            return fallbackSpan;

        foreach (var candidate in DocumentationCommentId.GetSymbolsForDeclarationId(declarationId, compilation)) {
            foreach (var location in candidate.Locations) {
                if (location.IsInSource && PathExtensions.Equals(location.SourceTree?.FilePath, filePath))
                    return location.GetLineSpan();
            }
        }

        currentClassLogger.Debug($"Could not locate symbol '{declarationId}' in '{filePath}'");
        return fallbackSpan;
    }

    public async Task<FileLinePositionSpan?> EmitDecompiledFileAsync(ISymbol symbol, Project project, CancellationToken cancellationToken) {
        var containingAssemblyName = symbol.ContainingAssembly?.Name;
        var typeName = symbol.GetTopLevelTypeFullName();
        if (containingAssemblyName == null || string.IsNullOrEmpty(typeName)) {
            currentClassLogger.Debug($"Cannot resolve containing type or assembly for symbol '{symbol}'");
            return null;
        }

        var outputFilePath = Path.Combine(decompiledCodeDirectory, project.Name, containingAssemblyName, typeName + ".cs");
        if (!temporaryDocuments.ContainsKey(outputFilePath) || !File.Exists(outputFilePath)) {
            var sourceText = await assemblyDecompiler.DecompileTypeAsync(symbol, project, cancellationToken);
            if (sourceText == null) {
                currentClassLogger.Debug($"Failed to decompile type '{typeName}' from assembly '{containingAssemblyName}'");
                return null;
            }

            FileSystemExtensions.WriteAllText(outputFilePath, sourceText);
            AddDocument(outputFilePath, project.Id);
            currentClassLogger.Debug($"Emit decompiled file: {outputFilePath}");
        }

        return await FindSymbolFileSpanAsync(symbol, outputFilePath, project.Id, cancellationToken);
    }
    public async Task<FileLinePositionSpan?> EmitCompilerGeneratedFileAsync(Location location, Project project, CancellationToken cancellationToken) {
        var documentPath = location.SourceTree?.FilePath;
        if (location.SourceTree == null || string.IsNullOrEmpty(documentPath))
            return null;

        // Generated files might be changed by compiler at any time, so skip cache here
        var outputFilePath = Path.Combine(generatedCodeDirectory, project.Name, documentPath);
        var sourceText = await location.SourceTree.GetTextAsync(cancellationToken);
        FileSystemExtensions.WriteAllText(outputFilePath, sourceText.ToString());
        AddDocument(outputFilePath, project.Id);

        currentClassLogger.Debug($"Emit source generated file: {outputFilePath}");
        return new FileLinePositionSpan(outputFilePath, location.GetLineSpan().Span);
    }

    public void UpdateSolution(Solution? solution) {
        var currentSolution = solution;
        foreach (var pair in temporaryDocuments) {
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
        if (temporaryDocuments.Remove(documentPath))
            currentClassLogger.Debug($"Document {documentPath} has been removed form {nameof(NavigationHost)} cache");
    }
    public void ClearCache() {
        temporaryDocuments.Clear();
    }

    private void AddDocument(string documentPath, ProjectId projectId) {
        temporaryDocuments.TryAdd(documentPath, projectId);
        UpdateSolution(Solution);
    }
}
