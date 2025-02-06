using DotRush.Roslyn.Server.Extensions;
using DotRush.Roslyn.Server.Services;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using DotRush.Roslyn.Common.Extensions;
using DotRush.Roslyn.Common;
using DotRush.Roslyn.Workspaces.Extensions;
using EmmyLua.LanguageServer.Framework.Server.Handler;
using EmmyLua.LanguageServer.Framework.Protocol.Message.DocumentSymbol;
using EmmyLua.LanguageServer.Framework.Protocol.Capabilities.Server;
using EmmyLua.LanguageServer.Framework.Protocol.Capabilities.Client.ClientCapabilities;

namespace DotRush.Roslyn.Server.Handlers.TextDocument;

public class DocumentSymbolHandler : DocumentSymbolHandlerBase {
    private readonly NavigationService navigationService;

    public DocumentSymbolHandler(NavigationService navigationService) {
        this.navigationService = navigationService;
    }

    public override void RegisterCapability(ServerCapabilities serverCapabilities, ClientCapabilities clientCapabilities) {
        serverCapabilities.DocumentSymbolProvider = true;
    }
    protected override Task<DocumentSymbolResponse> Handle(DocumentSymbolParams request, CancellationToken token) {
        return SafeExtensions.InvokeAsync(new DocumentSymbolResponse(new List<DocumentSymbol>()), async () => {
            var documentPath = request.TextDocument.Uri.FileSystemPath;
            var documentId = navigationService?.Solution?.GetDocumentIdsWithFilePathV2(documentPath).FirstOrDefault();
            var document = navigationService?.Solution?.GetDocument(documentId);
            if (documentId == null || document == null)
                return new DocumentSymbolResponse(new List<DocumentSymbol>());

            var semanticModel = await document.GetSemanticModelAsync(token);
            if (semanticModel == null)
                return new DocumentSymbolResponse(new List<DocumentSymbol>());

            var root = await semanticModel.SyntaxTree.GetRootAsync(token);
            if (root == null)
                return new DocumentSymbolResponse(new List<DocumentSymbol>());

            var documentSymbols = TraverseSyntaxTree(root.ChildNodes(), semanticModel);
            if (documentSymbols == null)
                return new DocumentSymbolResponse(new List<DocumentSymbol>());

            return new DocumentSymbolResponse(documentSymbols);
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