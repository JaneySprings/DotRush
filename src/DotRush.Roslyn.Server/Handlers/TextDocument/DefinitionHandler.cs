using DotRush.Roslyn.Common.Extensions;
using DotRush.Roslyn.Server.Containers;
using DotRush.Roslyn.Server.Extensions;
using DotRush.Roslyn.Server.Services;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace DotRush.Roslyn.Server.Handlers.TextDocument;

public class DefinitionHandler : DefinitionHandlerBase {
    private readonly NavigationService navigationService;

    public DefinitionHandler(NavigationService navigationService) {
        this.navigationService = navigationService;
    }

    protected override DefinitionRegistrationOptions CreateRegistrationOptions(DefinitionCapability capability, ClientCapabilities clientCapabilities) {
        return new DefinitionRegistrationOptions() {
            DocumentSelector = LanguageServer.SelectorForSourceCodeDocuments
        };
    }
    public override async Task<LocationOrLocationLinks?> Handle(DefinitionParams request, CancellationToken cancellationToken) {
        var result = await HandleCore(request, cancellationToken);
        if (result != null && !result.IsEmpty)
            return result.ToLocationOrLocationLinks();

        var documentIds = navigationService.Solution?.GetDocumentIdsWithFilePath(request.TextDocument.Uri.GetFileSystemPath());
        if (documentIds == null)
            return null;

        var isLocationsEmitted = false;
        foreach (var documentId in documentIds) {
            var document = navigationService.Solution?.GetDocument(documentId);
            if (document == null)
                continue;

            var sourceText = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
            var symbol = await SymbolFinder.FindSymbolAtPositionAsync(document, request.Position.ToOffset(sourceText), cancellationToken).ConfigureAwait(false);
            if (await navigationService.EmitSymbolLocationsAsync(symbol, document.Project, cancellationToken).ConfigureAwait(false))
                isLocationsEmitted = true;
        }

        if (isLocationsEmitted)
            result = await HandleCore(request, cancellationToken).ConfigureAwait(false);

        return result?.ToLocationOrLocationLinks();
    }
    private async Task<LocationsContainer?> HandleCore(DefinitionParams request, CancellationToken cancellationToken) {
        var documentIds = navigationService.Solution?.GetDocumentIdsWithFilePath(request.TextDocument.Uri.GetFileSystemPath());
        if (documentIds == null)
            return null;

        var result = new LocationsContainer();
        foreach (var documentId in documentIds) {
            var document = navigationService.Solution?.GetDocument(documentId);
            if (document == null)
                continue;

            var sourceText = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
            var symbol = await SymbolFinder.FindSymbolAtPositionAsync(document, request.Position.ToOffset(sourceText), cancellationToken).ConfigureAwait(false);
            if (symbol == null || symbol.Locations == null)
                continue;

            foreach (var location in symbol.Locations) {
                var filePath = location.SourceTree?.FilePath ?? string.Empty;
                if (LanguageExtensions.IsCompilerGeneratedFile(filePath))
                    filePath = await navigationService.EmitCompilerGeneratedLocationAsync(location, document.Project, cancellationToken).ConfigureAwait(false);

                if (string.IsNullOrEmpty(filePath))
                    continue;

                result.Add(location.ToLocation(filePath));
            }

            result.AddRange(symbol.Locations
                .Where(loc => File.Exists(loc.SourceTree?.FilePath))
                .Select(loc => loc.ToLocation()));
        }

        return result;
    }
}