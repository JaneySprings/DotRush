using DotRush.Common.Extensions;
using DotRush.Roslyn.Server.Extensions;
using DotRush.Roslyn.Server.Services;
using DotRush.Roslyn.Workspaces.Extensions;
using EmmyLua.LanguageServer.Framework.Protocol.Capabilities.Client.ClientCapabilities;
using EmmyLua.LanguageServer.Framework.Protocol.Capabilities.Server;
using EmmyLua.LanguageServer.Framework.Protocol.Message.TypeDefinition;
using EmmyLua.LanguageServer.Framework.Server.Handler;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;
using ProtocolModels = EmmyLua.LanguageServer.Framework.Protocol.Model;

namespace DotRush.Roslyn.Server.Handlers.TextDocument;

public class TypeDefinitionHandler : TypeDefinitionHandlerBase {
    private readonly NavigationService navigationService;

    public TypeDefinitionHandler(NavigationService navigationService) {
        this.navigationService = navigationService;
    }

    public override void RegisterCapability(ServerCapabilities serverCapabilities, ClientCapabilities clientCapabilities) {
        serverCapabilities.TypeDefinitionProvider = true;
    }
    protected override async Task<TypeDefinitionResponse?> Handle(TypeDefinitionParams request, CancellationToken cancellationToken) {
        var result = new NullableValueCollection<ProtocolModels.Location>();
        var decompiledResult = new NullableValueCollection<ProtocolModels.Location>();
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

            var typeSymbol = symbol.GetTypeSymbol();
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

        return new TypeDefinitionResponse(result.IsEmpty && !decompiledResult.IsEmpty
            ? decompiledResult.ToNonNullableList()
            : result.ToNonNullableList()
        );
    }
}