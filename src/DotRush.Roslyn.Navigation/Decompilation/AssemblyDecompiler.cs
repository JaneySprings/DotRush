using System.Reflection.PortableExecutable;
using System.Text;
using DotRush.Roslyn.Navigation.Extensions;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.Metadata;
using ICSharpCode.Decompiler.TypeSystem;
using Microsoft.CodeAnalysis;
using ISymbol = Microsoft.CodeAnalysis.ISymbol;

namespace DotRush.Roslyn.Navigation.Decompilation;

public class AssemblyDecompiler {
    public DecompilerSettings DecompilerSettings { get; set; } = new DecompilerSettings {
        ThrowOnAssemblyResolveErrors = false,
        RemoveDeadCode = false,
        RemoveDeadStores = false,
        ShowXmlDocumentation = true,
        UseNestedDirectoriesForNamespaces = false,
        UseDebugSymbols = true,
    };

    public async Task<string?> DecompileTypeAsync(ISymbol symbol, Project project, CancellationToken cancellationToken) {
        var typeName = symbol.GetTopLevelTypeFullName();
        if (string.IsNullOrEmpty(typeName))
            return null;

        var decompiler = await CreateDecompilerAsync(symbol.ContainingAssembly, project, cancellationToken);
        if (decompiler == null)
            return null;

        var fullTypeName = new FullTypeName(typeName);
        decompiler = ResolveTypeForwarding(decompiler, fullTypeName);

        var metadataFile = decompiler.TypeSystem.MainModule.MetadataFile;
        return new StringBuilder()
            .AppendLine($"#region Assembly {metadataFile.FullName}")
            .AppendLine($"// {metadataFile.FileName}")
            .AppendLine("#endregion")
            .AppendLine()
            .Append(decompiler.DecompileTypeAsString(fullTypeName))
            .ToString();
    }

    private async Task<CSharpDecompiler?> CreateDecompilerAsync(IAssemblySymbol? assemblySymbol, Project project, CancellationToken cancellationToken) {
        if (assemblySymbol == null)
            return null;

        var compilation = await project.GetCompilationAsync(cancellationToken);
        var peReference = compilation?.GetMetadataReference(assemblySymbol) as PortableExecutableReference;
        if (peReference?.FilePath == null)
            return null;

        // The compilation may reference a 'reference assembly' without method bodies - resolve the implementation assembly
        var module = new PEFile(peReference.FilePath, PEStreamOptions.PrefetchEntireImage);
        var resolver = new UniversalAssemblyResolver(project.OutputFilePath, false, module.DetectTargetFrameworkId(), module.DetectRuntimePack());
        var implementationAssemblyPath = resolver.FindAssemblyFile(AssemblyNameReference.Parse(assemblySymbol.Identity.GetDisplayName()));
        if (!string.IsNullOrEmpty(implementationAssemblyPath))
            module = new PEFile(implementationAssemblyPath, PEStreamOptions.PrefetchEntireImage);

        return new CSharpDecompiler(module, resolver, DecompilerSettings);
    }
    private CSharpDecompiler ResolveTypeForwarding(CSharpDecompiler decompiler, FullTypeName fullTypeName) {
        // The type may be forwarded to another assembly (e.g. System.String: System.Runtime -> System.Private.CoreLib)
        var type = decompiler.TypeSystem.FindType(fullTypeName.TopLevelTypeName).GetDefinition();
        if (type?.ParentModule != null && type.ParentModule != decompiler.TypeSystem.MainModule) {
            var parentModulePath = type.ParentModule.MetadataFile?.FileName;
            if (File.Exists(parentModulePath))
                return new CSharpDecompiler(parentModulePath, DecompilerSettings);
        }

        return decompiler;
    }
}
