using DotRush.Common.Extensions;
using DotRush.Roslyn.Server.Extensions;
using DotRush.Roslyn.Server.Services;
using DotRush.Roslyn.Workspaces.Extensions;
using EmmyLua.LanguageServer.Framework.Protocol.Capabilities.Client.ClientCapabilities;
using EmmyLua.LanguageServer.Framework.Protocol.Capabilities.Server;
using EmmyLua.LanguageServer.Framework.Protocol.Message.DocumentSymbol;
using EmmyLua.LanguageServer.Framework.Server.Handler;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SymbolKind = EmmyLua.LanguageServer.Framework.Protocol.Message.DocumentSymbol.SymbolKind;

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

            var syntaxTree = await document.GetSyntaxTreeAsync(token).ConfigureAwait(false);
            if (syntaxTree == null)
                return new DocumentSymbolResponse(new List<DocumentSymbol>());

            var root = await syntaxTree.GetRootAsync(token).ConfigureAwait(false);
            if (root == null)
                return new DocumentSymbolResponse(new List<DocumentSymbol>());

            var documentSymbols = TraverseSyntaxTree(root.ChildNodes());
            return new DocumentSymbolResponse(documentSymbols);
        });
    }

    private static List<DocumentSymbol> TraverseSyntaxTree(IEnumerable<SyntaxNode> nodes) {
        var result = new List<DocumentSymbol>();

        foreach (var node in nodes.OfType<MemberDeclarationSyntax>()) {
            if (node is BaseNamespaceDeclarationSyntax namespaceDeclaration) {
                result.Add(CreateSymbol(namespaceDeclaration.Name.ToString(), SymbolKind.Namespace, namespaceDeclaration, true));
            }
            else if (node is ClassDeclarationSyntax classDeclaration) {
                result.Add(CreateSymbol(classDeclaration.Identifier.Text, SymbolKind.Class, classDeclaration, true));
            }
            else if (node is StructDeclarationSyntax structDeclaration) {
                result.Add(CreateSymbol(structDeclaration.Identifier.Text, SymbolKind.Struct, structDeclaration, true));
            }
            else if (node is EnumDeclarationSyntax enumDeclaration) {
                result.Add(CreateSymbol(enumDeclaration.Identifier.Text, SymbolKind.Enum, enumDeclaration, true));
            }
            else if (node is InterfaceDeclarationSyntax interfaceDeclaration) {
                result.Add(CreateSymbol(interfaceDeclaration.Identifier.Text, SymbolKind.Interface, interfaceDeclaration, true));
            }
            else if (node is DelegateDeclarationSyntax delegateDeclaration) {
                result.Add(CreateSymbol(delegateDeclaration.Identifier.Text, SymbolKind.Function, delegateDeclaration, true));
            }
            else if (node is ConstructorDeclarationSyntax ctorDeclaration) {
                result.Add(CreateSymbol(ctorDeclaration.Identifier.Text, SymbolKind.Constructor, ctorDeclaration, true));
            }
            else if (node is MethodDeclarationSyntax methodDeclaration) {
                result.Add(CreateSymbol(methodDeclaration.Identifier.Text, SymbolKind.Method, methodDeclaration));
            }
            else if (node is PropertyDeclarationSyntax propDeclaration) {
                result.Add(CreateSymbol(propDeclaration.Identifier.Text, SymbolKind.Property, propDeclaration));
            }
            else if (node is IndexerDeclarationSyntax indexerDeclaration) {
                result.Add(CreateSymbol("this[]", SymbolKind.Property, indexerDeclaration));
            }
            else if (node is EventDeclarationSyntax eventDeclarationSyntax) {
                result.Add(CreateSymbol(eventDeclarationSyntax.Identifier.Text, SymbolKind.Event, eventDeclarationSyntax));
            }
            else if (node is EnumMemberDeclarationSyntax enumMemberDeclaration) {
                result.Add(CreateSymbol(enumMemberDeclaration.Identifier.Text, SymbolKind.EnumMember, enumMemberDeclaration));
            }
            else if (node is FieldDeclarationSyntax fieldDeclaration) {
                foreach (var variable in fieldDeclaration.Declaration.Variables) {
                    var kind = fieldDeclaration.Modifiers.Any(SyntaxKind.ConstKeyword) ? SymbolKind.Constant : SymbolKind.Field;
                    result.Add(CreateSymbol(variable.Identifier.Text, kind, fieldDeclaration));
                }
            }
        }
        return result;
    }
    private static string GetFormattedName(MemberDeclarationSyntax memberDeclaration, string name) {
        if (string.IsNullOrEmpty(name))
            name = "?";

        if (memberDeclaration is BaseMethodDeclarationSyntax baseMethodDeclaration) {
            var parameters = string.Join(", ", baseMethodDeclaration.ParameterList.Parameters.Select(p => p.Type?.ToString() ?? "?"));
            return $"{name}({parameters})";
        }

        return name;
    }
    private static DocumentSymbol CreateSymbol(string name, SymbolKind kind, MemberDeclarationSyntax memberDeclaration, bool includeChildren = false) {
        var range = memberDeclaration.GetLocation().ToRange();
        return new DocumentSymbol() {
            Name = GetFormattedName(memberDeclaration, name),
            Kind = kind,
            Range = range,
            SelectionRange = range,
            Children = includeChildren ? TraverseSyntaxTree(memberDeclaration.ChildNodes()) : null
        };
    }
}
