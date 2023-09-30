using DotRush.Server.Extensions;
using DotRush.Server.Services;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using CodeAnalysis = Microsoft.CodeAnalysis;

namespace DotRush.Server.Handlers;

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
            DocumentSelector = DocumentSelector.ForLanguage("csharp")
        };
    }

    public override async Task<Hover?> Handle(HoverParams request, CancellationToken cancellationToken) {
        return await ServerExtensions.SafeHandlerAsync<Hover?>(async () => {
            var documentIds = this.solutionService.Solution?.GetDocumentIdsWithFilePath(request.TextDocument.Uri.GetFileSystemPath());
            if (documentIds == null)
                return null;
            
            foreach (var documentId in documentIds) {
                var document = this.solutionService.Solution?.GetDocument(documentId);
                if (document == null)
                    continue;

                var sourceText = await document.GetTextAsync(cancellationToken);
                var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
                var offset = request.Position.ToOffset(sourceText);
                var symbol = await SymbolFinder.FindSymbolAtPositionAsync(document, offset);
                if (symbol == null || semanticModel == null)
                    continue;

                var displayString = symbol.Kind == CodeAnalysis.SymbolKind.NamedType 
                    ? symbol.ToDisplayString(DefaultFormat) 
                    : symbol.ToMinimalDisplayString(semanticModel, offset, MinimalFormat);

                return new Hover {
                    Contents = new MarkedStringsOrMarkupContent(new MarkupContent {
                        Kind = MarkupKind.Markdown,
                        Value = $"```csharp\n{displayString}\n```"
                    })
                };
            }

            return null;
        });
    }
}