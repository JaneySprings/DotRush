using DotRush.Server.Extensions;
using DotRush.Server.Services;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace DotRush.Server.Handlers;

public class TypeDefinitionHandler : TypeDefinitionHandlerBase {
    private SolutionService solutionService;

    public TypeDefinitionHandler(SolutionService solutionService) {
        this.solutionService = solutionService;
    }

    protected override TypeDefinitionRegistrationOptions CreateRegistrationOptions(TypeDefinitionCapability capability, ClientCapabilities clientCapabilities) {
        return new TypeDefinitionRegistrationOptions();
    }

    public override async Task<LocationOrLocationLinks> Handle(TypeDefinitionParams request, CancellationToken cancellationToken) {
        var document = this.solutionService.GetDocumentByPath(request.TextDocument.Uri.GetFileSystemPath());
        if (document == null)
            return new LocationOrLocationLinks();

        var sourceText = await document.GetTextAsync(cancellationToken);
        var symbol = await SymbolFinder.FindSymbolAtPositionAsync(document, request.Position.ToOffset(sourceText), cancellationToken);
        if (symbol == null)
            return new LocationOrLocationLinks();

        ITypeSymbol? typeSymbol = null;

        if (symbol is ILocalSymbol localSymbol) 
            typeSymbol = localSymbol.Type;
        else if (symbol is IFieldSymbol fieldSymbol)
            typeSymbol = fieldSymbol.Type;
        else if (symbol is IPropertySymbol propertySymbol)
            typeSymbol = propertySymbol.Type;
        else if (symbol is IParameterSymbol parameterSymbol)
            typeSymbol = parameterSymbol.Type;

        if (typeSymbol == null)
            return new LocationOrLocationLinks();

        return new LocationOrLocationLinks(typeSymbol.Locations.Select(loc => new LocationOrLocationLink(loc.ToLocation()!)));
    }
}