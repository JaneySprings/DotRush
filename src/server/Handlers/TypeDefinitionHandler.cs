using DotRush.Server.Containers;
using DotRush.Server.Extensions;
using DotRush.Server.Services;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using ProtocolModels = OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace DotRush.Server.Handlers;

public class TypeDefinitionHandler : TypeDefinitionHandlerBase {
    private readonly SolutionService solutionService;
    private readonly DecompilationService decompilationService;

    public TypeDefinitionHandler(SolutionService solutionService, DecompilationService decompilationService) {
        this.solutionService = solutionService;
        this.decompilationService = decompilationService;
    }

    protected override TypeDefinitionRegistrationOptions CreateRegistrationOptions(TypeDefinitionCapability capability, ClientCapabilities clientCapabilities) {
        return new TypeDefinitionRegistrationOptions();
    }

    public override async Task<LocationOrLocationLinks> Handle(TypeDefinitionParams request, CancellationToken cancellationToken) {
        var documentIds = this.solutionService.Solution?.GetDocumentIdsWithFilePath(request.TextDocument.Uri.GetFileSystemPath());
        if (documentIds == null)
            return new LocationOrLocationLinks();

        Document? document = null;
        ITypeSymbol? typeSymbol = null;

        var result = new LocationCollection();
        foreach (var documentId in documentIds) {
            document = this.solutionService.Solution?.GetDocument(documentId);
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
            var decompiledDocument = await this.decompilationService.DecompileAsync(typeSymbol, document.Project, cancellationToken);

            // TODO
            result.Add(new ProtocolModels.Location() {
                Uri = DocumentUri.From(decompiledDocument!.FilePath!),
                Range = new ProtocolModels.Range() {
                    Start = new ProtocolModels.Position(0, 0),
                    End = new ProtocolModels.Position(0, 0),
                },
            });
        }

        return result.ToLocationOrLocationLinks();
    }
}