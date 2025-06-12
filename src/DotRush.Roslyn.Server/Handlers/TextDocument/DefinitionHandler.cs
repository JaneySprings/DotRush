using DotRush.Common.Extensions;
using DotRush.Roslyn.Server.Extensions;
using DotRush.Roslyn.Server.Services;
using DotRush.Roslyn.Workspaces.Extensions;
using EmmyLua.LanguageServer.Framework.Protocol.Capabilities.Client.ClientCapabilities;
using EmmyLua.LanguageServer.Framework.Protocol.Capabilities.Server;
using EmmyLua.LanguageServer.Framework.Protocol.Message.Definition;
using EmmyLua.LanguageServer.Framework.Protocol.Model;
using EmmyLua.LanguageServer.Framework.Server.Handler;
using Microsoft.CodeAnalysis.FindSymbols;

namespace DotRush.Roslyn.Server.Handlers.TextDocument;

public class DefinitionHandler : DefinitionHandlerBase {
    private readonly NavigationService navigationService;

    public DefinitionHandler(NavigationService navigationService) {
        this.navigationService = navigationService;
    }

    public override void RegisterCapability(ServerCapabilities serverCapabilities, ClientCapabilities clientCapabilities) {
        serverCapabilities.DefinitionProvider = true;
    }
    protected override async Task<DefinitionResponse?> Handle(DefinitionParams request, CancellationToken cancellationToken) {
        var result = new NullableValueCollection<Location>();
        var decompiledResult = new NullableValueCollection<Location>();
        var isDecompiled = false;

        var documentIds = navigationService.Solution?.GetDocumentIdsWithFilePathV2(request.TextDocument.Uri.FileSystemPath);
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

        if (result.Count == 0 && !isDecompiled) {
            isDecompiled = true;
            goto handle;
        }

        return new DefinitionResponse(result.IsEmpty && !decompiledResult.IsEmpty
            ? decompiledResult.ToNonNullableList()
            : result.ToNonNullableList()
        );
    }
}