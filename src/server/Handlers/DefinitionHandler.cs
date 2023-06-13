using DotRush.Server.Extensions;
using DotRush.Server.Services;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace DotRush.Server.Handlers;

public class DefinitionHandler : DefinitionHandlerBase {
    private SolutionService solutionService;

    public DefinitionHandler(SolutionService solutionService) {
        this.solutionService = solutionService;
    }

    protected override DefinitionRegistrationOptions CreateRegistrationOptions(DefinitionCapability capability, ClientCapabilities clientCapabilities) {
        return new DefinitionRegistrationOptions();
    }

    public override async Task<LocationOrLocationLinks> Handle(DefinitionParams request, CancellationToken cancellationToken) {
        var documentIds = this.solutionService.Solution?.GetDocumentIdsWithFilePath(request.TextDocument.Uri.GetFileSystemPath());
        if (documentIds == null)
            return new LocationOrLocationLinks();

        var result = new List<LocationOrLocationLink>();
        foreach (var documentId in documentIds) {
            var document = this.solutionService.Solution?.GetDocument(documentId);
            if (document == null)
                continue;

            var sourceText = await document.GetTextAsync(cancellationToken);
            var symbol = await SymbolFinder.FindSymbolAtPositionAsync(document, request.Position.ToOffset(sourceText), cancellationToken);
            if (symbol == null || this.solutionService.Solution == null) 
                continue;
            
            var definition = await SymbolFinder.FindSourceDefinitionAsync(symbol, this.solutionService.Solution, cancellationToken);
            // if (definition == null) 
            //     definition = await FindSourceDefinitionWithDecompilerAsync(symbol, document.Project, cancellationToken);
            if (definition == null)
                continue;
            
            result.AddRange(definition.Locations.Select(loc => new LocationOrLocationLink(loc.ToLocation()!)));
        }

        return new LocationOrLocationLinks(result);
    }

    // private async Task<ISymbol?> FindSourceDefinitionWithDecompilerAsync(ISymbol symbol, Project project, CancellationToken cancellationToken) {
    //     var decompilationCacheDirectory = Path.Combine(Path.GetDirectoryName(project.FilePath!)!, ".dotrush", "decompilation");
    //     var targetFilePath = Path.Combine(decompilationCacheDirectory, $"{symbol.ToDisplayString()}.cs");
        
    //     if (File.Exists(targetFilePath))
    //         return await FindSourceDefinitionWithFilePathAsync(symbol, targetFilePath, project, cancellationToken);
        
    //     var compilation = await project.GetCompilationAsync();
    //     var assembly = compilation?.GetMetadataReference(symbol.ContainingAssembly) as PortableExecutableReference;

    //     var resolver = new UniversalAssemblyResolver(assembly?.FilePath, false, string.Empty);
    //     var decompiler = new CSharpDecompiler(assembly?.FilePath, resolver, new DecompilerSettings());
    //     decompiler.AstTransforms.Add(new EscapeInvalidIdentifiers());

    //     try {
    //         var decompiled = decompiler.DecompileTypeAsString(new TypeSystem.FullTypeName(symbol.ToDisplayString()));
    //         if (decompiled == null)
    //             return null;

    //         if (!Directory.Exists(decompilationCacheDirectory))
    //             Directory.CreateDirectory(decompilationCacheDirectory);

    //         File.WriteAllText(targetFilePath, decompiled);
    //         return await FindSourceDefinitionWithFilePathAsync(symbol, targetFilePath, project, cancellationToken);
    //     } catch {
    //         return null;
    //     }
    // }

    // private async Task<ISymbol?> FindSourceDefinitionWithFilePathAsync(ISymbol symbol, string filePath, Project project, CancellationToken cancellationToken) {
    //     var documentContent = File.ReadAllText(filePath);
    //     var folders = project.GetFolders(filePath);
    //     var updates = project.AddDocument(Path.GetFileName(filePath), documentContent, folders, filePath);

    //     return await SymbolFinder.FindSourceDefinitionAsync(symbol, updates.Project.Solution, cancellationToken);
    // }
}