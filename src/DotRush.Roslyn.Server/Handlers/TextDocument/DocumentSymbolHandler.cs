using DotRush.Roslyn.Server.Extensions;
using DotRush.Roslyn.Server.Services;
using Microsoft.CodeAnalysis;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using DotRush.Roslyn.Common.Extensions;
using DotRush.Roslyn.Common;

namespace DotRush.Roslyn.Server.Handlers.TextDocument;

public class DocumentSymbolHandler : DocumentSymbolHandlerBase {
    

    private readonly NavigationService navigationService;

    public DocumentSymbolHandler(NavigationService navigationService) {
        this.navigationService = navigationService;
    }

    protected override DocumentSymbolRegistrationOptions CreateRegistrationOptions(DocumentSymbolCapability capability, ClientCapabilities clientCapabilities) {
        return new DocumentSymbolRegistrationOptions() {
            DocumentSelector = LanguageServer.SelectorForSourceCodeDocuments
        };
    }
    public override Task<SymbolInformationOrDocumentSymbolContainer?> Handle(DocumentSymbolParams request, CancellationToken cancellationToken) {
        return SafeExtensions.InvokeAsync(async () => {
            var documentPath = request.TextDocument.Uri.GetFileSystemPath();
            var documentId = navigationService?.Solution?.GetDocumentIdsWithFilePath(documentPath).FirstOrDefault();
            var document = navigationService?.Solution?.GetDocument(documentId);
            if (documentId == null || document == null)
                return null;

            var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
            if (semanticModel == null)
                return null;

            var root = await semanticModel.SyntaxTree.GetRootAsync(cancellationToken);
            if (root == null)
                return null;

            var documentSymbols = TraverseSyntaxTree(root.ChildNodes(), semanticModel);
            if (documentSymbols == null)
                return null;

            return new SymbolInformationOrDocumentSymbolContainer(documentSymbols.Select(it => new SymbolInformationOrDocumentSymbol(it)));
        });
    }

    private static List<DocumentSymbol>? TraverseSyntaxTree(IEnumerable<SyntaxNode> nodes, SemanticModel semanticModel) {
        var result = new List<DocumentSymbol>();
        foreach (var node in nodes) {
            if (node is not MemberDeclarationSyntax)
                continue;

            ISymbol? symbol = null;
            if (node is FieldDeclarationSyntax fieldDeclaration && fieldDeclaration.Declaration.Variables.Any())
                symbol = semanticModel.GetDeclaredSymbol(fieldDeclaration.Declaration.Variables.First());
            else
                symbol = semanticModel.GetDeclaredSymbol(node);

            if (string.IsNullOrEmpty(symbol?.Name))
                continue;

            result.Add(new DocumentSymbol() {
                Name = symbol.ToDisplayString(DisplayFormat.Member),
                Kind = symbol.ToSymbolKind(),
                Range = node.GetLocation().ToRange(),
                SelectionRange = node.GetLocation().ToRange(),
                Children = TraverseSyntaxTree(node.ChildNodes(), semanticModel)
            });
        }
        return result;
    }
}