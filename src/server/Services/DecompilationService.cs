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
    private const string DecompiledProjectName = "__decompilation__";

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

        var documentPath = GetDecompiledDocumentPath(symbol, fullName, project);
        var documentId = solutionService.Solution?.GetDocumentIdsWithFilePath(documentPath).FirstOrDefault();
        if (documentId != null)
            return solutionService.Solution?.GetDocument(documentId);

        var metadataProject = solutionService.Solution?.Projects.FirstOrDefault(it => it.Name == DecompiledProjectName);
        if (metadataProject == null) {
            metadataProject = project.Solution
                .AddProject(DecompiledProjectName, $"{DecompiledProjectName}.dll", LanguageNames.CSharp)
                // .WithCompilationOptions(project.CompilationOptions)
                .WithMetadataReferences(project.MetadataReferences);
        }
    
        var compilation = await project.GetCompilationAsync(cancellationToken);
        var metadataReference = compilation?.GetMetadataReference(symbol.ContainingAssembly);
        var document = DecompileDocument(fullName, documentPath, metadataReference, metadataProject);
       

        return document;
    }

    private Document? DecompileDocument(string fullName, string documentPath, MetadataReference? metadataReference, Project metadataProject) {
        var assemblyLocation = (metadataReference as PortableExecutableReference)?.FilePath;
        if (assemblyLocation == null || metadataReference == null)
            return null;

        var file = new PEFile(assemblyLocation, PEStreamOptions.PrefetchEntireImage);
        var resolver = new UniversalAssemblyResolver(file.FullName, false, null);
        var decompiler = new CSharpDecompiler(file, resolver, new DecompilerSettings());
        decompiler.AstTransforms.Add(new EscapeInvalidIdentifiers());

        var fullTypeName = new FullTypeName(fullName);
        var assemblyInfoBuilder = new StringBuilder();

        assemblyInfoBuilder.AppendLine($"#region Assembly {metadataReference.Display}");
        assemblyInfoBuilder.AppendLine($"// {assemblyLocation}");
        assemblyInfoBuilder.AppendLine("#endregion");
        assemblyInfoBuilder.AppendLine();
        assemblyInfoBuilder.Append(decompiler.DecompileTypeAsString(fullTypeName));

        return metadataProject.AddDocument(Path.GetFileName(documentPath), SourceText.From(assemblyInfoBuilder.ToString()), null, documentPath);
    }

    private string GetDecompiledDocumentPath(ISymbol symbol, string fullName, Project project) {
        var topLevelSymbol = symbol.GetTopLevelContainingNamedType();
        return Path.Combine(DecompiledProjectName, project.Name, topLevelSymbol.ContainingAssembly.Name, $"{fullName}.cs");
    }
}