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

            var displayDictionary = new Dictionary<string, List<string>>();
            var documentation = string.Empty;
            foreach (var documentId in documentIds) {
                var document = navigationService.Solution?.GetDocument(documentId);
                if (document == null)
                    continue;

                var sourceText = await document.GetTextAsync(token).ConfigureAwait(false);
                var offset = request.Position.ToOffset(sourceText);
                var symbol = await SymbolFinder.FindSymbolAtPositionAsync(document, offset).ConfigureAwait(false);
                if (symbol == null)
                    continue;

                if (symbol is IAliasSymbol aliasSymbol)
                    symbol = aliasSymbol.Target;

                var format = symbol.Kind == SymbolKind.NamedType || symbol.Kind == SymbolKind.Namespace ? DisplayFormat.Default : DisplayFormat.Minimal;
                var displayString = symbol.ToDisplayString(format);
                if (!displayDictionary.ContainsKey(displayString))
                    displayDictionary[displayString] = new List<string>();

                displayDictionary[displayString].Add(document.Project.GetTargetFramework());

                if (string.IsNullOrEmpty(documentation))
                    documentation = symbol.GetInheritedDocumentationCommentXml();
            }

            if (displayDictionary.Count == 1) {
                return new HoverResponse {
                    Contents = new MarkupContent {
                        Kind = MarkupKind.Markdown,
                        Value = MarkdownExtensions.CreateDocumentation(displayDictionary.Keys.First(), documentation, "csharp")
                    }
                };
            }

            if (displayDictionary.Count > 1) {
                var builder = new StringBuilder();
                displayDictionary.ForEach(kv => builder.AppendLine($"{kv.Key}  ({string.Join(", ", kv.Value)})"));

                return new HoverResponse {
                    Contents = new MarkupContent {
                        Kind = MarkupKind.Markdown,
                        Value = MarkdownExtensions.CreateDocumentation(builder.ToString(), "csharp")
                    }
                };
            }

            return null;
        });
    }
}