using DotRush.Roslyn.Server.Containers;
using DotRush.Roslyn.Server.Extensions;
using DotRush.Roslyn.Server.Services;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace DotRush.Roslyn.Server.Handlers.TextDocument;

public class TypeDefinitionHandler : TypeDefinitionHandlerBase {
    private readonly WorkspaceService solutionService;
    private readonly DecompilationService decompilationService;

    public TypeDefinitionHandler(WorkspaceService solutionService, DecompilationService decompilationService) {
        this.solutionService = solutionService;
        this.decompilationService = decompilationService;
    }

    protected override TypeDefinitionRegistrationOptions CreateRegistrationOptions(TypeDefinitionCapability capability, ClientCapabilities clientCapabilities) {
        return new TypeDefinitionRegistrationOptions() {
            DocumentSelector = LanguageServer.SelectorForSourceCodeDocuments
        };
    }

    public override async Task<LocationOrLocationLinks?> Handle(TypeDefinitionParams request, CancellationToken cancellationToken) {
        var documentIds = solutionService.Solution?.GetDocumentIdsWithFilePath(request.TextDocument.Uri.GetFileSystemPath());
        if (documentIds == null)
            return null;

        Document? document = null;
        ITypeSymbol? typeSymbol = null;

        var result = new LocationsContainer();
        foreach (var documentId in documentIds) {
            document = solutionService.Solution?.GetDocument(documentId);
            if (document == null)
                continue;

            var sourceText = await document.GetTextAsync(cancellationToken);
            var symbol = await SymbolFinder.FindSymbolAtPositionAsync(document, request.Position.ToOffset(sourceText), cancellationToken);
            if (symbol == null)
                continue;

            if (symbol is ILocalSymbol localSymbol)
                typeSymbol = localSymbol.Type;
            else if (symbol is IFieldSymbol fieldSymbol)
                typeSymbol = fieldSymbol.Type;
            else if (symbol is IPropertySymbol propertySymbol)
                typeSymbol = propertySymbol.Type;
            else if (symbol is IParameterSymbol parameterSymbol)
                typeSymbol = parameterSymbol.Type;

            if (typeSymbol == null || typeSymbol.Locations == null)
                continue;

            result.AddRange(typeSymbol.Locations.Select(loc => loc.ToLocation()));
        }

        if (result.IsEmpty && document != null && typeSymbol != null) {
            var locations = await decompilationService.DecompileAsync(typeSymbol, document.Project, cancellationToken);
            if (locations != null)
                result.AddRange(locations);
        }

        return result.ToLocationOrLocationLinks();
    }
}