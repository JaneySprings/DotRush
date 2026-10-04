using DotRush.Protocol.Handlers;
using DotRush.Protocol.Models;
using DotRush.Roslyn.CodeAnalysis.Extensions;
using DotRush.Roslyn.Server.Extensions;
using DotRush.Roslyn.Server.Services;
using DotRush.Roslyn.Workspaces.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SymbolKind = DotRush.Protocol.Models.SymbolKind;

namespace DotRush.Roslyn.Server.Handlers.TextDocument;

public class DocumentSymbolHandler : DocumentSymbolHandlerBase {
    private readonly NavigationService navigationService;

    public DocumentSymbolHandler(NavigationService navigationService) {
        this.navigationService = navigationService;
    }

    public override void RegisterCapability(ServerCapabilities serverCapabilities) {
        serverCapabilities.DocumentSymbolProvider = true;
    }
    protected override async Task<List<DocumentSymbol>> Handle(DocumentSymbolParams request, CancellationToken token) {
        var documentPath = request.TextDocument.Uri.FileSystemPath;
        var solution = navigationService.GetRequiredSolution(documentPath);
        var documentId = solution?.GetDocumentIdsWithFilePathV2(documentPath).FirstOrDefault();
        var document = solution?.GetDocument(documentId);
        if (documentId == null || document == null)
            return new List<DocumentSymbol>();

        var syntaxTree = await document.GetSyntaxTreeAsync(token);
        if (syntaxTree == null)
            return new List<DocumentSymbol>();

        var root = await syntaxTree.GetRootAsync(token);
        if (root == null)
            return new List<DocumentSymbol>();

        var documentSymbols = TraverseSyntaxTree(root.ChildNodes());
        return documentSymbols;
    }

    private static List<DocumentSymbol> TraverseSyntaxTree(IEnumerable<SyntaxNode> nodes) {
        var result = new List<DocumentSymbol>();

        foreach (var node in nodes.OfType<MemberDeclarationSyntax>()) {
            var kind = node.ToSymbolKind();
            if (kind == SymbolKind.Null)
                continue;

            if (node is BaseFieldDeclarationSyntax fieldDeclaration) {
                foreach (var variable in fieldDeclaration.Declaration.Variables)
                    result.Add(CreateSymbol(variable.ToDisplayString(), kind, fieldDeclaration));
                continue;
            }

            result.Add(CreateSymbol(node.ToDisplayString(), kind, node));
        }
        return result;
    }
    private static DocumentSymbol CreateSymbol(string name, SymbolKind kind, MemberDeclarationSyntax memberDeclaration) {
        var range = memberDeclaration.GetLocation().ToRange();

        if (memberDeclaration.AttributeLists.Count > 0) {
            var sourceText = memberDeclaration.SyntaxTree.GetText();
            if (sourceText != null) {
                var startLine = memberDeclaration.AttributeLists.FullSpan.ToRange(sourceText).End.Line;
                range = new DocumentRange(
                    new Position(startLine, range.Start.Character),
                    new Position(range.End.Line, range.End.Character)
                );
            }
        }

        return new DocumentSymbol() {
            Name = name,
            Kind = kind,
            Range = range,
            SelectionRange = range,
            Children = memberDeclaration is BaseNamespaceDeclarationSyntax or BaseTypeDeclarationSyntax
                ? TraverseSyntaxTree(memberDeclaration.ChildNodes())
                : null
        };
    }
}
