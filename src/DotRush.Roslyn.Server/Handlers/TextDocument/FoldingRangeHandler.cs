using DotRush.Roslyn.Server.Services;
using DotRush.Roslyn.Server.Extensions;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using DotRush.Common.Extensions;
using DotRush.Roslyn.Workspaces.Extensions;
using EmmyLua.LanguageServer.Framework.Server.Handler;
using EmmyLua.LanguageServer.Framework.Protocol.Message.FoldingRange;
using EmmyLua.LanguageServer.Framework.Protocol.Capabilities.Server;
using EmmyLua.LanguageServer.Framework.Protocol.Capabilities.Client.ClientCapabilities;

namespace DotRush.Roslyn.Server.Handlers.TextDocument;

public class FoldingRangeHandler : FoldingRangeHandlerBase {
    private readonly NavigationService navigationService;

    public FoldingRangeHandler(NavigationService navigationService) {
        this.navigationService = navigationService;
    }

    public override void RegisterCapability(ServerCapabilities serverCapabilities, ClientCapabilities clientCapabilities) {
        serverCapabilities.FoldingRangeProvider = true;
    }
    protected override Task<FoldingRangeResponse> Handle(FoldingRangeParams request, CancellationToken token) {
        return SafeExtensions.InvokeAsync(new FoldingRangeResponse(new List<FoldingRange>()), async () => {
            var result = new List<FoldingRange>();

            var documentIds = navigationService.Solution?.GetDocumentIdsWithFilePathV2(request.TextDocument.Uri.FileSystemPath);
            var documentId = documentIds?.FirstOrDefault();
            var document = navigationService.Solution?.GetDocument(documentId);
            if (document == null)
                return new FoldingRangeResponse(result);

            var sourceText = await document.GetTextAsync(token).ConfigureAwait(false);
            var syntaxTree = await document.GetSyntaxTreeAsync(token).ConfigureAwait(false);
            if (syntaxTree == null)
                return new FoldingRangeResponse(result);

            var root = await syntaxTree.GetRootAsync(token).ConfigureAwait(false);

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

                result.Add(new FoldingRange { StartLine = (uint)startLine, EndLine = (uint)endLine });
            }

            var directiveNodes = root.DescendantTrivia().Where(it => it.IsDirective).Select(it => it.GetStructure());
            result.AddRange(GetFoldingDirectivesOfType<IfDirectiveTriviaSyntax, EndIfDirectiveTriviaSyntax>(directiveNodes, sourceText));
            result.AddRange(GetFoldingDirectivesOfType<ElseDirectiveTriviaSyntax, EndIfDirectiveTriviaSyntax>(directiveNodes, sourceText));
            result.AddRange(GetFoldingDirectivesOfType<ElifDirectiveTriviaSyntax, EndIfDirectiveTriviaSyntax>(directiveNodes, sourceText));
            result.AddRange(GetFoldingDirectivesOfType<RegionDirectiveTriviaSyntax, EndRegionDirectiveTriviaSyntax>(directiveNodes, sourceText));

            return new FoldingRangeResponse(result);
        });
    }

    private static List<FoldingRange> GetFoldingDirectivesOfType<TStart, TEnd>(IEnumerable<SyntaxNode?> nodes, SourceText sourceText) where TStart : DirectiveTriviaSyntax where TEnd : DirectiveTriviaSyntax {
        var result = new List<FoldingRange>();
        var foldingStack = new Stack<FoldingRange>();
        
        foreach (var node in nodes) {
            if (node is TStart startDirectiveSyntax) {
                var startRange = startDirectiveSyntax.Span.ToRange(sourceText);
                foldingStack.Push(new FoldingRange {
                    Kind = FoldingRangeKind.Region,
                    StartLine = (uint)startRange.Start.Line
                });
            }
            if (node is TEnd endDirectiveSyntax) {
                if (foldingStack.Count == 0)
                    continue;

                var foldingRange = foldingStack.Pop();
                var endRange = endDirectiveSyntax.Span.ToRange(sourceText);
                foldingRange.EndLine = (uint)endRange.End.Line;
                result.Add(foldingRange);
            }
        }

        return result;
    }
}