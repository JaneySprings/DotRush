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

    public async Task<CSharpDecompiler?> CreateDecompilerAsync(IAssemblySymbol assemblySymbol, Project project, CancellationToken cancellationToken) {
        var peReference = await GetPEReference(assemblySymbol, project, cancellationToken);
        if (peReference == null || peReference.FilePath == null)
            return null;

        var module = new PEFile(peReference.FilePath, PEStreamOptions.PrefetchEntireImage);
        var resolver = new UniversalAssemblyResolver(project.OutputFilePath, false, module.DetectTargetFrameworkId(), module.DetectRuntimePack());
        var resolvedAssemblyPath = resolver.FindAssemblyFile(new AssemblyReference(assemblySymbol));
        if (!string.IsNullOrEmpty(resolvedAssemblyPath))
            module = new PEFile(resolvedAssemblyPath, PEStreamOptions.PrefetchEntireImage);

        return new CSharpDecompiler(module, resolver, DecompilerSettings);
    }
    public string DecompileType(CSharpDecompiler decompiler, ISymbol typeSymbol) {
        var typeName = typeSymbol.GetNamedTypeFullName();
        if (string.IsNullOrEmpty(typeName))
            throw new InvalidOperationException("Type name is empty");

        var fullTypeName = new FullTypeName(typeName);
        decompiler = ValidateDecompilerTypeSystem(decompiler, fullTypeName);

        var metadataFile = decompiler.TypeSystem.MainModule.MetadataFile;
        var sourceText = new StringBuilder()
            .AppendLine($"#region Assembly {metadataFile.FullName}")
            .AppendLine($"// {metadataFile.FileName}")
            .AppendLine("#endregion")
            .AppendLine()
            .Append(decompiler.DecompileTypeAsString(fullTypeName));

        return sourceText.ToString();
    }

    private async Task<PortableExecutableReference?> GetPEReference(IAssemblySymbol assemblySymbol, Project project, CancellationToken cancellationToken) {
        var compilation = await project.GetCompilationAsync(cancellationToken);
        var metadataReference = compilation?.GetMetadataReference(assemblySymbol);
        return metadataReference as PortableExecutableReference;
    }
    private CSharpDecompiler ValidateDecompilerTypeSystem(CSharpDecompiler decompiler, FullTypeName fullTypeName) {
        var type = decompiler.TypeSystem.FindType(fullTypeName.TopLevelTypeName).GetDefinition();

        if (type?.ParentModule != null && type.ParentModule != decompiler.TypeSystem.MainModule) {
            var parentModulePath = type.ParentModule.MetadataFile?.FileName;
            if (File.Exists(parentModulePath))
                return new CSharpDecompiler(parentModulePath, DecompilerSettings);
        }

        return decompiler;
    }

    class AssemblyReference : IAssemblyReference {
        public string Name { get; init; }
        public string FullName { get; init; }
        public Version? Version { get; init; }
        public string? Culture { get; init; }
        public byte[]? PublicKeyToken { get; init; }
        public bool IsRetargetable { get; init; }
        public bool IsWindowsRuntime => false;

        public AssemblyReference(IAssemblySymbol assemblySymbol) {
            Name = assemblySymbol.Identity.Name;
            FullName = assemblySymbol.Identity.GetDisplayName();
            Version = assemblySymbol.Identity.Version;
            Culture = assemblySymbol.Identity.CultureName;
            PublicKeyToken = assemblySymbol.Identity.PublicKeyToken.ToArray();
            IsRetargetable = assemblySymbol.Identity.IsRetargetable;
        }
    }
}