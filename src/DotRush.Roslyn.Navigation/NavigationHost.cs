using DotRush.Roslyn.Common.Extensions;
using DotRush.Roslyn.Common.Logging;
using DotRush.Roslyn.Navigation.Decompilation;
using DotRush.Roslyn.Navigation.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using FileSystemExtensions = DotRush.Roslyn.Common.Extensions.FileSystemExtensions;

namespace DotRush.Roslyn.Navigation;

public class NavigationHost {
    public string DecompiledCodeDirectory { get; private set; }
    public string GeneratedCodeDirectory { get; private set; }
    public Solution? Solution { get; set; }

    public NavigationHost() {
        var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
        DecompiledCodeDirectory = Path.Combine(baseDirectory, "_decompiled_");
        GeneratedCodeDirectory = Path.Combine(baseDirectory, "_generated_");
    }

    public async Task<string?> EmitDecompiledFileAsync(ISymbol symbol, Project project, CancellationToken cancellationToken) {
        var symbolFullName = symbol.GetNamedTypeFullName();
        if (string.IsNullOrEmpty(symbolFullName))
            return null;

        using var decompiler = new AssemblyDecompiler();
        var isMetadataCollected = await decompiler.CollectAssemblyMetadataAsync(symbol.ContainingAssembly, project, cancellationToken).ConfigureAwait(false);
        if (!isMetadataCollected)
            return null;

        var syntaxTree = decompiler.DecompileType(symbolFullName);
        var outputFilePath = Path.Combine(DecompiledCodeDirectory, project.Name, symbolFullName + ".cs");
        
        FileSystemExtensions.WriteAllText(outputFilePath, syntaxTree.ToString());
        CreateDocument(outputFilePath, null, project);
        return outputFilePath;
    }
    public async Task<string?> EmitCompilerGeneratedFileAsync(Location location, Project project, CancellationToken cancellationToken) {
        var documentPath = location.SourceTree?.FilePath;
        if (location.SourceTree == null || documentPath == null || !LanguageExtensions.IsCompilerGeneratedFile(documentPath))
            return null;
        
        var outputFilePath = Path.Combine(GeneratedCodeDirectory, project.Name, documentPath);
        var sourceText = await location.SourceTree.GetTextAsync(cancellationToken).ConfigureAwait(false);
        FileSystemExtensions.WriteAllText(outputFilePath, sourceText.ToString());
        CreateDocument(outputFilePath, sourceText, project);

        CurrentSessionLogger.Debug($"[SourceGenerator]: Created file: {outputFilePath}");
        return outputFilePath;
    }

    private void CreateDocument(string documentPath, SourceText? sourceText, Project project) {
        if (project.Documents.Any(d => FileSystemExtensions.PathEquals(d.FilePath, documentPath)))
            return;
        if (sourceText == null)
            sourceText = SourceText.From(File.ReadAllText(documentPath));
        
        var documentName = Path.GetFileName(documentPath);
        var document = project.AddDocument(documentName, sourceText, filePath: documentPath);
        Solution = document.Project.Solution;
    }
}