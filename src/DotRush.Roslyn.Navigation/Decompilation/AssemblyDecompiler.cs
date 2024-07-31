using System.Reflection.PortableExecutable;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.Metadata;

namespace DotRush.Roslyn.Navigation.Decompilation;

public class AssemblyDecompiler {
    private readonly UniversalAssemblyResolver resolver;
    private readonly DecompilerSettings settings;

    public AssemblyDecompiler(string assemblyPath) {
        using var peFile = new PEFile(assemblyPath, PEStreamOptions.PrefetchEntireImage);
        resolver = new UniversalAssemblyResolver(peFile.FullName, false, peFile.DetectTargetFrameworkId(), peFile.DetectRuntimePack());
        settings = new DecompilerSettings {
            ThrowOnAssemblyResolveErrors = false,
            RemoveDeadCode = false,
            RemoveDeadStores = false,
            ShowXmlDocumentation = true,
            UseSdkStyleProjectFormat = peFile.DetectTargetFrameworkId() != null,
            UseNestedDirectoriesForNamespaces = false,
        };

        AddSearchDirectory(Path.GetDirectoryName(assemblyPath));
    }

    public void AddSearchDirectory(string? directory) {
        resolver.AddSearchDirectory(directory);
    }
}