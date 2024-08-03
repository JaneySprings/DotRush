using DotRush.Roslyn.Server.Services;
using DotRush.Roslyn.Server.Extensions;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using DotRush.Roslyn.Common.Extensions;

namespace DotRush.Roslyn.Server.Handlers.TextDocument;

public class FoldingRangeHandler : FoldingRangeHandlerBase {
    private readonly NavigationService navigationService;

    public FoldingRangeHandler(NavigationService navigationService) {
        this.navigationService = navigationService;
    }

    protected override FoldingRangeRegistrationOptions CreateRegistrationOptions(FoldingRangeCapability capability, ClientCapabilities clientCapabilities) {
        return new FoldingRangeRegistrationOptions {
            DocumentSelector = LanguageServer.SelectorForSourceCodeDocuments,
        };
    }

    public override Task<Container<FoldingRange>?> Handle(FoldingRangeRequestParam request, CancellationToken cancellationToken) {
        return SafeExtensions.InvokeAsync(async () => {
            var documentIds = navigationService.Solution?.GetDocumentIdsWithFilePath(request.TextDocument.Uri.GetFileSystemPath());
            var documentId = documentIds?.FirstOrDefault();
            var document = navigationService.Solution?.GetDocument(documentId);
            if (document == null)
                return null;

            var sourceText = await document.GetTextAsync(cancellationToken);
            var syntaxTree = await document.GetSyntaxTreeAsync(cancellationToken);
            if (syntaxTree == null)
                return null;

            var result = new List<FoldingRange>();
            var root = await syntaxTree.GetRootAsync(cancellationToken);

            var commonNodes = root.DescendantNodes().Where(node =>
                node is BaseTypeDeclarationSyntax
                || node is BaseMethodDeclarationSyntax
                || node is BasePropertyDeclarationSyntax
                || node is BaseNamespaceDeclarationSyntax
                || node is StatementSyntax
            );
            foreach (var node in commonNodes) {
                if (node is BlockSyntax blockSyntax && blockSyntax.Parent is BaseMethodDeclarationSyntax)
                    continue;

                var range = node.Span.ToRange(sourceText);
                var startLine = range.Start.Line;
                var endLine = range.End.Line;

                if (node is MemberDeclarationSyntax memberDeclarationSyntax && memberDeclarationSyntax.AttributeLists.Count > 0)
                    startLine = memberDeclarationSyntax.AttributeLists.FullSpan.ToRange(sourceText).End.Line;

                result.Add(new FoldingRange { StartLine = startLine, EndLine = endLine });
            }

            var directiveNodes = root.DescendantTrivia().Where(it => it.IsDirective).Select(it => it.GetStructure());
            result.AddRange(GetFoldingDirectivesOfType<IfDirectiveTriviaSyntax, EndIfDirectiveTriviaSyntax>(directiveNodes, sourceText));
            result.AddRange(GetFoldingDirectivesOfType<ElseDirectiveTriviaSyntax, EndIfDirectiveTriviaSyntax>(directiveNodes, sourceText));
            result.AddRange(GetFoldingDirectivesOfType<ElifDirectiveTriviaSyntax, EndIfDirectiveTriviaSyntax>(directiveNodes, sourceText));
            result.AddRange(GetFoldingDirectivesOfType<RegionDirectiveTriviaSyntax, EndRegionDirectiveTriviaSyntax>(directiveNodes, sourceText));

            return new Container<FoldingRange>(result);
        });
    }

    private static List<FoldingRange> GetFoldingDirectivesOfType<TStart, TEnd>(IEnumerable<SyntaxNode?> nodes, SourceText sourceText) where TStart : DirectiveTriviaSyntax where TEnd : DirectiveTriviaSyntax {
        var result = new List<FoldingRange>();
        var startDirectivesSyntax = nodes.OfType<TStart>();
        foreach (var startDirectiveSyntax in startDirectivesSyntax) {
            var startRange = startDirectiveSyntax.Span.ToRange(sourceText);
            var endDirectiveSyntax = nodes.OfType<TEnd>().FirstOrDefault(it => it.Span.Start > startDirectiveSyntax.Span.End);
            if (endDirectiveSyntax == null)
                continue;

            var endRange = endDirectiveSyntax.Span.ToRange(sourceText);
            result.Add(new FoldingRange {
                Kind = FoldingRangeKind.Region,
                StartLine = startRange.Start.Line,
                EndLine = endRange.End.Line
            });
        }

        return result;
    }
}