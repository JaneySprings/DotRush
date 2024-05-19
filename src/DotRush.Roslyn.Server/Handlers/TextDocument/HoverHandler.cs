using System.Text;
using DotRush.Roslyn.Common.Extensions;
using DotRush.Roslyn.Server.Extensions;
using DotRush.Roslyn.Server.Services;
using DotRush.Roslyn.Workspaces.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using SymbolKind = Microsoft.CodeAnalysis.SymbolKind;

namespace DotRush.Roslyn.Server.Handlers.TextDocument;

public class HoverHandler : HoverHandlerBase {
    private readonly WorkspaceService solutionService;
    public static readonly SymbolDisplayFormat DefaultFormat = SymbolDisplayFormat.FullyQualifiedFormat
        .WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Omitted)
        .WithMiscellaneousOptions(SymbolDisplayMiscellaneousOptions.None)
        .WithMiscellaneousOptions(SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers);

    public static readonly SymbolDisplayFormat MinimalFormat = SymbolDisplayFormat.MinimallyQualifiedFormat
        .WithMemberOptions(SymbolDisplayMemberOptions.IncludeParameters
            | SymbolDisplayMemberOptions.IncludeType
            | SymbolDisplayMemberOptions.IncludeRef
            | SymbolDisplayMemberOptions.IncludeContainingType
            | SymbolDisplayMemberOptions.IncludeConstantValue
        );


    public HoverHandler(WorkspaceService solutionService) {
        this.solutionService = solutionService;
    }

    protected override HoverRegistrationOptions CreateRegistrationOptions(HoverCapability capability, ClientCapabilities clientCapabilities) {
        return new HoverRegistrationOptions {
            DocumentSelector = LanguageServer.SelectorForSourceCodeDocuments,
        };
    }

    public override Task<Hover?> Handle(HoverParams request, CancellationToken cancellationToken) {
        return SafeExtensions.InvokeAsync(async () => {
            var documentIds = solutionService.Solution?.GetDocumentIdsWithFilePath(request.TextDocument.Uri.GetFileSystemPath());
            if (documentIds == null)
                return null;

            var displayStrings = new Dictionary<string, List<string>>();
            foreach (var documentId in documentIds) {
                var document = solutionService.Solution?.GetDocument(documentId);
                if (document == null)
                    continue;

                var sourceText = await document.GetTextAsync(cancellationToken);
                var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
                var offset = request.Position.ToOffset(sourceText);
                var symbol = await SymbolFinder.FindSymbolAtPositionAsync(document, offset);
                if (symbol == null || semanticModel == null)
                    continue;

                if (symbol is IAliasSymbol aliasSymbol)
                    symbol = aliasSymbol.Target;

                var displayString = symbol.Kind == SymbolKind.NamedType || symbol.Kind == SymbolKind.Namespace
                    ? symbol.ToDisplayString(DefaultFormat)
                    : symbol.ToMinimalDisplayString(semanticModel, offset, MinimalFormat);

                if (!displayStrings.ContainsKey(displayString))
                    displayStrings.Add(displayString, new List<string>());

                displayStrings[displayString].Add(document.Project.GetTargetFramework());
            }

            if (displayStrings.Count == 1) {
                return new Hover {
                    Contents = new MarkedStringsOrMarkupContent(new MarkedString("csharp", displayStrings.Keys.First()))
                };
            }

            if (displayStrings.Count > 1) {
                var builder = new StringBuilder();
                foreach (var pair in displayStrings) {
                    var frameworks = string.Join(", ", pair.Value);
                    builder.AppendLine($"```csharp\n{pair.Key}  ({frameworks})\n```");
                }

                return new Hover {
                    Contents = new MarkedStringsOrMarkupContent(new MarkupContent {
                        Kind = MarkupKind.Markdown,
                        Value = builder.ToString()
                    })
                };
            }

            return null;
        });
    }
}