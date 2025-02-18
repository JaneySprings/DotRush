using System.Text;
using DotRush.Common.Extensions;
using DotRush.Roslyn.CodeAnalysis;
using DotRush.Roslyn.Server.Extensions;
using DotRush.Roslyn.Server.Services;
using DotRush.Roslyn.Workspaces.Extensions;
using EmmyLua.LanguageServer.Framework.Protocol.Capabilities.Client.ClientCapabilities;
using EmmyLua.LanguageServer.Framework.Protocol.Capabilities.Server;
using EmmyLua.LanguageServer.Framework.Protocol.Message.Hover;
using EmmyLua.LanguageServer.Framework.Protocol.Model.Markup;
using EmmyLua.LanguageServer.Framework.Server.Handler;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;
using SymbolKind = Microsoft.CodeAnalysis.SymbolKind;

namespace DotRush.Roslyn.Server.Handlers.TextDocument;

public class HoverHandler : HoverHandlerBase {
    private readonly NavigationService navigationService;

    public HoverHandler(NavigationService navigationService) {
        this.navigationService = navigationService;
    }

    public override void RegisterCapability(ServerCapabilities serverCapabilities, ClientCapabilities clientCapabilities) {
        serverCapabilities.HoverProvider = true;
    }
    protected override Task<HoverResponse?> Handle(HoverParams request, CancellationToken token) {
        return SafeExtensions.InvokeAsync(async () => {
            var documentIds = navigationService.Solution?.GetDocumentIdsWithFilePathV2(request.TextDocument.Uri.FileSystemPath);
            if (documentIds == null)
                return null;

            var displayStrings = new Dictionary<string, List<string>>();
            foreach (var documentId in documentIds) {
                var document = navigationService.Solution?.GetDocument(documentId);
                if (document == null)
                    continue;

                var sourceText = await document.GetTextAsync(token).ConfigureAwait(false);
                var semanticModel = await document.GetSemanticModelAsync(token).ConfigureAwait(false);
                var offset = request.Position.ToOffset(sourceText);
                var symbol = await SymbolFinder.FindSymbolAtPositionAsync(document, offset).ConfigureAwait(false);
                if (symbol == null || semanticModel == null)
                    continue;

                if (symbol is IAliasSymbol aliasSymbol)
                    symbol = aliasSymbol.Target;

                var displayString = symbol.Kind == SymbolKind.NamedType || symbol.Kind == SymbolKind.Namespace
                    ? symbol.ToDisplayString(DisplayFormat.Default)
                    : symbol.ToMinimalDisplayString(semanticModel, offset, DisplayFormat.Minimal);

                if (!displayStrings.ContainsKey(displayString))
                    displayStrings.Add(displayString, new List<string>());

                displayStrings[displayString].Add(document.Project.GetTargetFramework());
            }

            if (displayStrings.Count == 1) {
                return new HoverResponse { Contents = new MarkupContent {
                    Kind = MarkupKind.Markdown,
                    Value = MarkdownExtensions.Create(displayStrings.Keys.First(), "csharp")
                }};
            }

            if (displayStrings.Count > 1) {
                var builder = new StringBuilder();
                foreach (var pair in displayStrings) {
                    var frameworks = string.Join(", ", pair.Value);
                    builder.AppendLine(MarkdownExtensions.Create($"{pair.Key}  ({frameworks})", "csharp"));
                }

                return new HoverResponse { Contents = new MarkupContent {
                    Kind = MarkupKind.Markdown,
                    Value = builder.ToString()
                }};
            }

            return null;
        });
    }
}