using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.Metadata;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using FullTypeName = ICSharpCode.Decompiler.TypeSystem.FullTypeName;
using System.Reflection.PortableExecutable;
using DotRush.Roslyn.Server.Extensions;
using System.Text;
using ProtocolModels = OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Microsoft.CodeAnalysis.CSharp;
using OmniSharp.Extensions.LanguageServer.Protocol;
using DotRush.Roslyn.Common.Extensions;

namespace DotRush.Roslyn.Server.Services;

public class DecompilationService {
    private const string DecompiledProjectName = "_decompilation_";

    public string DecompilationDirectory { get; private set; }

    private readonly IConfigurationService configurationService;
    private readonly ILanguageServerFacade serverFacade;
    private readonly WorkspaceService solutionService;


    public DecompilationService(ILanguageServerFacade serverFacade, WorkspaceService solutionService, IConfigurationService configurationService) {
        DecompilationDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, DecompiledProjectName);

        this.serverFacade = serverFacade;
        this.solutionService = solutionService;
        this.configurationService = configurationService;
    }

    public async Task<IEnumerable<ProtocolModels.Location>?> DecompileAsync(ISymbol symbol, Project project, CancellationToken cancellationToken) {
        var locations = new List<ProtocolModels.Location>();
        var namedType = symbol.GetNamedTypeSymbol();
        var fullName = namedType.GetFullReflectionName();
        if (string.IsNullOrEmpty(fullName))
            return null;

        var documentPath = GetDecompiledDocumentPath(fullName);
        if (!File.Exists(documentPath)) {
            var compilation = await project.GetCompilationAsync(cancellationToken);
            var metadataReference = compilation?.GetMetadataReference(symbol.ContainingAssembly);
            if (!DecompileDocument(fullName, documentPath, metadataReference, symbol.ContainingAssembly))
                return null;
        }

        var symbolFinder = new PlainTextSymbolFinder(symbol);
        var sourceText = SourceText.From(File.ReadAllText(documentPath));
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceText, cancellationToken: cancellationToken);
        var root = await syntaxTree.GetRootAsync(cancellationToken);

        symbolFinder.Visit(root);
        locations.AddRange(symbolFinder.Locations.Select(loc => new ProtocolModels.Location() {
            Uri = DocumentUri.FromFileSystemPath(documentPath),
            Range = loc.ToRange(sourceText),
        }));

        if (locations.Count == 0) {
            locations.Add(new ProtocolModels.Location() {
                Uri = DocumentUri.FromFileSystemPath(documentPath),
                Range = new ProtocolModels.Range() {
                    Start = new ProtocolModels.Position(0, 0),
                    End = new ProtocolModels.Position(0, 0),
                },
            });
        }

        return locations;
    }

    private static bool DecompileDocument(string fullName, string documentPath, MetadataReference? metadataReference, IAssemblySymbol? assemblySymbol) {
        return SafeExtensions.Invoke(false, () => {
            var assemblyLocation = (metadataReference as PortableExecutableReference)?.FilePath;
            if (assemblyLocation == null || metadataReference == null)
                return false;

            var module = new PEFile(assemblyLocation, PEStreamOptions.PrefetchEntireImage);
            var resolver = new UniversalAssemblyResolver(module.FullName, false, module.Metadata.DetectTargetFrameworkId());
            var decompiler = new CSharpDecompiler(module, resolver, new DecompilerSettings() {
                ThrowOnAssemblyResolveErrors = false,
                RemoveDeadCode = false,
                RemoveDeadStores = false,
                ShowXmlDocumentation = true,
                UseSdkStyleProjectFormat = module.DetectTargetFrameworkId() != null,
                UseNestedDirectoriesForNamespaces = false,
            });

            var fullTypeName = new FullTypeName(fullName);
            var assemblyInfoBuilder = new StringBuilder();

            assemblyInfoBuilder.AppendLine($"#region Assembly {assemblySymbol}");
            assemblyInfoBuilder.AppendLine($"// {assemblyLocation}");
            assemblyInfoBuilder.AppendLine("#endregion");
            assemblyInfoBuilder.AppendLine();
            assemblyInfoBuilder.Append(decompiler.DecompileTypeAsString(fullTypeName));

            File.WriteAllText(documentPath, assemblyInfoBuilder.ToString());
            return true;
        });
    }

    private string GetDecompiledDocumentPath(string fullName) {
        var typeFullName = fullName.Split(".");
        var typeName = typeFullName.Last();
        var typeDirectory = Path.Combine(typeFullName.SkipLast(1).ToArray());
        var targetDirectory = Path.Combine(DecompilationDirectory, typeDirectory);

        if (!Directory.Exists(targetDirectory))
            Directory.CreateDirectory(targetDirectory);

        return Path.Combine(targetDirectory, $"{typeName}.cs");
    }
}
