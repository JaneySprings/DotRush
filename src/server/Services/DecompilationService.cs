using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.Metadata;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using FullTypeName = ICSharpCode.Decompiler.TypeSystem.FullTypeName;
using System.Reflection.PortableExecutable;
using DotRush.Server.Extensions;
using System.Text;

namespace DotRush.Server.Services;

public class DecompilationService {
    private const string DecompiledProjectName = "_decompilation_";

    public string DecompilationDirectory { get; private set;}
    
    private readonly ConfigurationService configurationService;
    private readonly ILanguageServerFacade serverFacade;
    private readonly SolutionService solutionService;


    public DecompilationService(ILanguageServerFacade serverFacade, SolutionService solutionService, ConfigurationService configurationService) {
        DecompilationDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, DecompiledProjectName);

        this.serverFacade = serverFacade;
        this.solutionService = solutionService;
        this.configurationService = configurationService;
    }

    public async Task<Document?> DecompileAsync(ISymbol symbol, Project project, CancellationToken cancellationToken) {
        var namedType = symbol.GetNamedTypeSymbol();
        var fullName = namedType.GetFullReflectionName();
        if (string.IsNullOrEmpty(fullName))
            return null;

        var documentPath = GetDecompiledDocumentPath(fullName);
        var documentId = solutionService.Solution?.GetDocumentIdsWithFilePath(documentPath).FirstOrDefault();
        if (documentId != null)
            return solutionService.Solution?.GetDocument(documentId);

        var metadataProject = solutionService.Solution?.Projects.FirstOrDefault(it => it.Name == DecompiledProjectName);
        if (metadataProject == null) {
            metadataProject = project.Solution
                .AddProject(DecompiledProjectName, $"{DecompiledProjectName}.dll", LanguageNames.CSharp)
                // .WithCompilationOptions(project.CompilationOptions)
                .WithMetadataReferences(project.MetadataReferences);
            solutionService.Solution = metadataProject.Solution;
        }
    
        var compilation = await project.GetCompilationAsync(cancellationToken);
        var metadataReference = compilation?.GetMetadataReference(symbol.ContainingAssembly);
        var document = DecompileDocument(fullName, documentPath, metadataReference, symbol.ContainingAssembly, metadataProject);

        return document;
    }

    private Document? DecompileDocument(string fullName, string documentPath, MetadataReference? metadataReference, IAssemblySymbol? assemblySymbol, Project metadataProject) {
        var assemblyLocation = (metadataReference as PortableExecutableReference)?.FilePath;
        if (assemblyLocation == null || metadataReference == null)
            return null;

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
        return metadataProject.AddDocument(Path.GetFileName(documentPath), SourceText.From(assemblyInfoBuilder.ToString()), null, documentPath);
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