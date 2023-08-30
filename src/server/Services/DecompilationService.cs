using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.CSharp.Transforms;
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

    private readonly ConfigurationService configurationService;
    private readonly ILanguageServerFacade serverFacade;
    private readonly SolutionService solutionService;


    public DecompilationService(ILanguageServerFacade serverFacade, SolutionService solutionService, ConfigurationService configurationService) {
        this.serverFacade = serverFacade;
        this.solutionService = solutionService;
        this.configurationService = configurationService;
    }

    public async Task<Document?> DecompileAsync(ISymbol symbol, Project project, CancellationToken cancellationToken) {
        var namedType = symbol.GetNamedTypeSymbol();
        var fullName = namedType.GetFullReflectionName();

        var documentPath = GetDecompiledDocumentPath(symbol, fullName, project.FilePath);
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

        var file = new PEFile(assemblyLocation, PEStreamOptions.PrefetchEntireImage);
        var resolver = new UniversalAssemblyResolver(file.FullName, false, null);
        var decompiler = new CSharpDecompiler(file, resolver, new DecompilerSettings() {
            DecompileMemberBodies = false,
            ShowXmlDocumentation = true,
            AsyncAwait = true,
        });
        decompiler.AstTransforms.Add(new EscapeInvalidIdentifiers());

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

    private string GetDecompiledDocumentPath(ISymbol symbol, string fullName, string? projectPath) {
        var topLevelSymbol = symbol.GetTopLevelContainingNamedType();
        var projectDirectory = Path.GetDirectoryName(projectPath) ?? string.Empty;
        var decompilationDirectory = Path.Combine(projectDirectory, ".dotrush", DecompiledProjectName, topLevelSymbol.ContainingAssembly.Name);
        if (!Directory.Exists(decompilationDirectory))
            Directory.CreateDirectory(decompilationDirectory);

        return Path.Combine(decompilationDirectory, $"{fullName}.cs");
    }
}