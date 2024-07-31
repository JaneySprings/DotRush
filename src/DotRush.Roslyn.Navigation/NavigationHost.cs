using DotRush.Roslyn.Common.Extensions;
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

    public async Task<bool> EmitSymbolLocationsAsync(ISymbol symbol, Project project, CancellationToken cancellationToken) {
        // var symbolFullName = symbol.GetNamedTypeSymbol().GetFullReflectionName();
        // if (string.IsNullOrEmpty(symbolFullName))
        //     return false;

        // foreach (var location in symbol.Locations) {
        //     var documentPath = location.SourceTree?.FilePath;
        //     if (documentPath == null || !documentPath.EndsWith(".sg.cs", StringComparison.OrdinalIgnoreCase))
        //         continue;

        //     await EmitCompilerGeneratedCodeAsync(documentPath, project, cancellationToken);
        // }

        return true;
    }

    public async Task<string?> EmitCompilerGeneratedLocationAsync(Location location, Project project, CancellationToken cancellationToken) {
        var documentPath = location.SourceTree?.FilePath;
        if (location.SourceTree == null || documentPath == null || !LanguageExtensions.IsCompilerGeneratedFile(documentPath))
            return null;
        
        var outputFilePath = Path.Combine(GeneratedCodeDirectory, project.Name, documentPath);
        var sourceText = await location.SourceTree.GetTextAsync(cancellationToken).ConfigureAwait(false);
        FileSystemExtensions.WriteAllText(outputFilePath, sourceText.ToString());
        CreateDocument(outputFilePath, sourceText, project);
        return outputFilePath;
    }


    private void CreateDocument(string documentPath, SourceText? sourceText, Project project) {
        if (sourceText == null)
            sourceText = SourceText.From(File.ReadAllText(documentPath));
        
        var documentName = Path.GetFileName(documentPath);
        var document = project.AddDocument(documentName, sourceText, filePath: documentPath);
        Solution = document.Project.Solution;
    }
}