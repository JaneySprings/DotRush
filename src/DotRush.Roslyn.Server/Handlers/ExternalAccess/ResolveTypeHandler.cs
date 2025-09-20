using System.Text.Json;
using System.Text.Json.Serialization;
using DotRush.Common;
using DotRush.Common.Extensions;
using DotRush.Roslyn.Server.Services;
using DotRush.Roslyn.Workspaces.Extensions;
using EmmyLua.LanguageServer.Framework;
using EmmyLua.LanguageServer.Framework.Protocol.Capabilities.Client.ClientCapabilities;
using EmmyLua.LanguageServer.Framework.Protocol.Capabilities.Server;
using EmmyLua.LanguageServer.Framework.Protocol.JsonRpc;
using EmmyLua.LanguageServer.Framework.Server;
using EmmyLua.LanguageServer.Framework.Server.Handler;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Text;

namespace DotRush.Roslyn.Server.Handlers.ExternalAccess;

public class ResolveTypeHandler : IJsonHandler {
    private readonly WorkspaceService workspaceService;
    private static readonly SymbolDisplayFormat displayFormat = new SymbolDisplayFormat(
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers | SymbolDisplayMiscellaneousOptions.UseSpecialTypes
    );

    public ResolveTypeHandler(WorkspaceService workspaceService) {
        this.workspaceService = workspaceService;
    }

    protected Task<TypeResponse?> Handle(ResolveTypeParams? request, CancellationToken token) {
        return SafeExtensions.InvokeAsync(async () => {
            var documentId = workspaceService.Solution?.GetDocumentIdsWithFilePathV2(request?.Location?.FileName)?.FirstOrDefault();
            var document = workspaceService.Solution?.GetDocument(documentId);
            if (document == null || request == null)
                return null;

            var sourceText = await document.GetTextAsync();
            var result = new TypeResponse();
            ISymbol? symbol = null;
            if (request.IsHover && request.Location != null) {
                var offset = sourceText.Lines.GetPosition(new LinePosition(request.Location.Line - 1, request.Location.Column - 1));
                symbol = await SymbolFinder.FindSymbolAtPositionAsync(document, offset + 1, CancellationToken.None);
            }
            else {
                //TODO
            }

            if (symbol is INamespaceSymbol namespaceSymbol)
                result.FullTypeName = namespaceSymbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);
            else if (symbol is INamedTypeSymbol namedTypeSymbol && namedTypeSymbol.TypeKind != TypeKind.Dynamic)
                result.FullTypeName = namedTypeSymbol.ToDisplayString(displayFormat);

            return result;
        });
    }

    public void RegisterHandler(LSPCommunicationBase lspCommunication) {
        lspCommunication.AddRequestHandler("dotrush/resolveType", async delegate (RequestMessage message, CancellationToken token) {
            var request = message.Params?.Deserialize<ResolveTypeParams>(JsonSerializerConfig.Options);
            return JsonSerializer.SerializeToDocument(await Handle(request, token).ConfigureAwait(false));
        });
    }
    public void RegisterCapability(ServerCapabilities serverCapabilities, ClientCapabilities clientCapabilities) {
    }
    public void RegisterDynamicCapability(LanguageServer server, ClientCapabilities clientCapabilities) {
    }
}

public class ResolveTypeParams {
    [JsonPropertyName("identifierName")] public string? IdentifierName { get; set; }
    [JsonPropertyName("sourceLocation")] public SourceLocation? Location { get; set; }
    [JsonPropertyName("hoverContext")] public bool IsHover { get; set; }
}
public class SourceLocation {
    [JsonPropertyName("fileName")] public string? FileName { get; set; }
    [JsonPropertyName("line")] public int Line { get; set; }
    [JsonPropertyName("column")] public int Column { get; set; }
}
public class TypeResponse {
    [JsonPropertyName("fullTypeName")] public string? FullTypeName { get; set; }
}