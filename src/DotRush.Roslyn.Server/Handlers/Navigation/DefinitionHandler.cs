using DotRush.Server.Extensions;
using DotRush.Server.Services;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace DotRush.Server.Handlers;

public class DefinitionHandler : DefinitionHandlerBase {
    private readonly WorkspaceService solutionService;
    private readonly DecompilationService decompilationService;

    public DefinitionHandler(WorkspaceService solutionService, DecompilationService decompilationService) {
        this.solutionService = solutionService;
        this.decompilationService = decompilationService;
    }

    protected override DefinitionRegistrationOptions CreateRegistrationOptions(DefinitionCapability capability, ClientCapabilities clientCapabilities) {
        return new DefinitionRegistrationOptions() {
            DocumentSelector = LanguageServer.SelectorForSourceCodeDocuments
        };
    }

    public override async Task<LocationOrLocationLinks?> Handle(DefinitionParams request, CancellationToken cancellationToken) {
        var documentIds = this.solutionService.Solution?.GetDocumentIdsWithFilePath(request.TextDocument.Uri.GetFileSystemPath());
        if (documentIds == null)
            return null;

        Document? document = null;
        ISymbol? symbol = null;

        var result = new LocationCollection();
        foreach (var documentId in documentIds) {
            document = this.solutionService.Solution?.GetDocument(documentId);
            if (document == null)
                continue;

            var sourceText = await document.GetTextAsync(cancellationToken);
            symbol = await SymbolFinder.FindSymbolAtPositionAsync(document, request.Position.ToOffset(sourceText), cancellationToken);
            if (symbol == null || symbol.Locations == null) 
                continue;

            result.AddRange(symbol.Locations.Select(loc => loc.ToLocation()));
        }

        if (result.IsEmpty && document != null && symbol != null) {
            var locations = await this.decompilationService.DecompileAsync(symbol, document.Project, cancellationToken);
            if (locations != null) 
                result.AddRange(locations);
        }
    
        return result.ToLocationOrLocationLinks();
    }
}