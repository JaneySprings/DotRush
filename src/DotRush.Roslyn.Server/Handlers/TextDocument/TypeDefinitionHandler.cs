using DotRush.Roslyn.Server.Containers;
using DotRush.Roslyn.Server.Extensions;
using DotRush.Roslyn.Server.Services;
using DotRush.Roslyn.Workspaces.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace DotRush.Roslyn.Server.Handlers.TextDocument;

public class TypeDefinitionHandler : TypeDefinitionHandlerBase {
    private readonly NavigationService navigationService;

    public TypeDefinitionHandler(NavigationService navigationService) {
        this.navigationService = navigationService;
    }

    protected override TypeDefinitionRegistrationOptions CreateRegistrationOptions(TypeDefinitionCapability capability, ClientCapabilities clientCapabilities) {
        return new TypeDefinitionRegistrationOptions() {
            DocumentSelector = LanguageServer.SelectorForSourceCodeDocuments
        };
    }

    public override async Task<LocationOrLocationLinks?> Handle(TypeDefinitionParams request, CancellationToken cancellationToken) {
        var result = new LocationsContainer();
        var decompiledResult = new LocationsContainer();
        var isDecompiled = false;

        var documentIds = navigationService.Solution?.GetDocumentIdsWithFilePathV2(request.TextDocument.Uri.GetFileSystemPath());
        if (documentIds == null)
            return null;

        handle:
        foreach (var documentId in documentIds) {
            var document = navigationService.Solution?.GetDocument(documentId);
            if (document == null)
                continue;

            var sourceText = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
            var symbol = await SymbolFinder.FindSymbolAtPositionAsync(document, request.Position.ToOffset(sourceText), cancellationToken).ConfigureAwait(false);
            if (symbol == null)
                continue;

            ITypeSymbol? typeSymbol = null;
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

            foreach (var location in typeSymbol.Locations) {
                if (location.IsInMetadata && !isDecompiled) {
                    var decompiledFilePath = await navigationService.EmitDecompiledFileAsync(typeSymbol, document.Project, cancellationToken).ConfigureAwait(false);
                    decompiledResult.Add(PositionExtensions.ToDecompiledUnknownLocation(decompiledFilePath));
                    continue;
                }

                if (!location.IsInSource || location.SourceTree == null)
                    continue;
                
                var filePath = location.SourceTree?.FilePath ?? string.Empty;
                if (!File.Exists(filePath))
                    filePath = await navigationService.EmitCompilerGeneratedFileAsync(location, document.Project, cancellationToken).ConfigureAwait(false);

                result.Add(location.ToLocation(filePath));
            }
        }

        if (result.IsEmpty && !isDecompiled) {
            isDecompiled = true;
            goto handle;
        }

        return result.IsEmpty && !decompiledResult.IsEmpty 
            ? decompiledResult.ToLocationOrLocationLinks()
            : result.ToLocationOrLocationLinks();
    }
}