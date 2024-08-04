using System.Reflection.PortableExecutable;
using DotRush.Roslyn.Navigation.Extensions;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.CSharp.Syntax;
using ICSharpCode.Decompiler.Metadata;
using ICSharpCode.Decompiler.TypeSystem;
using Microsoft.CodeAnalysis;
using ISymbol = Microsoft.CodeAnalysis.ISymbol;
using SyntaxTree = ICSharpCode.Decompiler.CSharp.Syntax.SyntaxTree;

namespace DotRush.Roslyn.Navigation.Decompilation;

public class AssemblyDecompiler : IDisposable {
    private readonly DecompilerSettings settings = new DecompilerSettings {
        ThrowOnAssemblyResolveErrors = false,
        RemoveDeadCode = false,
        RemoveDeadStores = false,
        ShowXmlDocumentation = true,
        UseNestedDirectoriesForNamespaces = false,
    };

    private UniversalAssemblyResolver? resolver;
    private PEFile? module;

    public async Task<bool> CollectAssemblyMetadataAsync(IAssemblySymbol assemblySymbol, Project project, CancellationToken cancellationToken) {
        var compilation = await project.GetCompilationAsync(cancellationToken);
        var metadataReference = compilation?.GetMetadataReference(assemblySymbol);
        var assemblyPath = (metadataReference as PortableExecutableReference)?.FilePath;
        if (assemblyPath == null || metadataReference == null)
            return false;

        module = new PEFile(assemblyPath, PEStreamOptions.PrefetchEntireImage);
        resolver = new UniversalAssemblyResolver(assemblyPath, false, module.DetectTargetFrameworkId(), module.DetectRuntimePack());
        resolver.AddSearchDirectory(Path.GetDirectoryName(project.OutputFilePath));
        resolver.AddSearchDirectory(Path.GetDirectoryName(assemblyPath));
        return true;
    }
    public SyntaxTree DecompileType(ISymbol typeSymbol) {
        var typeName = typeSymbol.GetNamedTypeFullName();
        if (resolver == null || module == null)
            throw new InvalidOperationException("Assembly metadata is not collected");
        if (string.IsNullOrEmpty(typeName))
            throw new InvalidOperationException("Type name is empty");
        
        var decompiler = new CSharpDecompiler(module, resolver, settings);
        var fullTypeName = new FullTypeName(typeName);
        var result = decompiler.DecompileType(fullTypeName);

        result.InsertChildBefore(result.Children.First(), new PreProcessorDirective(PreProcessorDirectiveType.Region, $"Assembly {module.FullName}"), Roles.PreProcessorDirective);
        result.InsertChildAfter(result.Children.First(), new Comment(" " + module.FileName), Roles.Comment);
        result.InsertChildAfter(result.Children.Skip(1).First(), new PreProcessorDirective(PreProcessorDirectiveType.Endregion, "\n"), Roles.PreProcessorDirective);
        return result;
    }

    public void Dispose() {
        module?.Dispose();
    }
}