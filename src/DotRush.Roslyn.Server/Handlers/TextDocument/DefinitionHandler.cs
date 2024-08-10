using DotRush.Roslyn.Server.Containers;
using DotRush.Roslyn.Server.Extensions;
using DotRush.Roslyn.Server.Services;
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
        var result = new LocationsContainer();
        var decompiledResult = new LocationsContainer();
        var isDecompiled = false;

        var documentIds = navigationService.Solution?.GetDocumentIdsWithFilePath(request.TextDocument.Uri.GetFileSystemPath());
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

            foreach (var location in symbol.Locations) {
                if (location.IsInMetadata && !isDecompiled) {
                    var decompiledFilePath = await navigationService.EmitDecompiledFileAsync(symbol, document.Project, cancellationToken).ConfigureAwait(false);
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