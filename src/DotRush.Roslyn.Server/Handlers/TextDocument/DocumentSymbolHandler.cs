using DotRush.Protocol.Handlers;
using DotRush.Protocol.Models;
using DotRush.Roslyn.Server.Extensions;
using DotRush.Roslyn.Server.Services;
using DotRush.Roslyn.Workspaces.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
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
            if (node is BaseNamespaceDeclarationSyntax namespaceDeclaration) {
                result.Add(CreateSymbol(namespaceDeclaration.Name.ToString(), SymbolKind.Namespace, namespaceDeclaration, true));
            }
            else if (node is ExtensionBlockDeclarationSyntax extensionDeclaration) {
                result.Add(CreateSymbol($"extension({extensionDeclaration.ParameterList?.Parameters.FirstOrDefault()?.Type})", SymbolKind.Class, extensionDeclaration, true));
            }
            else if (node is BaseTypeDeclarationSyntax typeDeclaration) {
                switch (typeDeclaration.Kind()) {
                    case SyntaxKind.StructDeclaration:
                    case SyntaxKind.RecordStructDeclaration:
                        result.Add(CreateSymbol(typeDeclaration.Identifier.Text, SymbolKind.Struct, typeDeclaration, true));
                        break;
                    case SyntaxKind.InterfaceDeclaration:
                        result.Add(CreateSymbol(typeDeclaration.Identifier.Text, SymbolKind.Interface, typeDeclaration, true));
                        break;
                    case SyntaxKind.EnumDeclaration:
                        result.Add(CreateSymbol(typeDeclaration.Identifier.Text, SymbolKind.Enum, typeDeclaration, true));
                        break;
                    default:
                        result.Add(CreateSymbol(typeDeclaration.Identifier.Text, SymbolKind.Class, typeDeclaration, true));
                        break;
                }
            }
            else if (node is DelegateDeclarationSyntax delegateDeclaration) {
                result.Add(CreateSymbol(delegateDeclaration.Identifier.Text, SymbolKind.Function, delegateDeclaration, true));
            }
            else if (node is ConstructorDeclarationSyntax ctorDeclaration) {
                result.Add(CreateSymbol(ctorDeclaration.Identifier.Text, SymbolKind.Constructor, ctorDeclaration, true));
            }
            else if (node is DestructorDeclarationSyntax destructorDeclaration) {
                result.Add(CreateSymbol($"~{destructorDeclaration.Identifier.Text}", SymbolKind.Method, destructorDeclaration));
            }
            else if (node is MethodDeclarationSyntax methodDeclaration) {
                result.Add(CreateSymbol(methodDeclaration.Identifier.Text, SymbolKind.Method, methodDeclaration));
            }
            else if (node is OperatorDeclarationSyntax operatorDeclaration) {
                result.Add(CreateSymbol($"operator {operatorDeclaration.OperatorToken.Text}", SymbolKind.Operator, operatorDeclaration));
            }
            else if (node is ConversionOperatorDeclarationSyntax conversionDeclaration) {
                result.Add(CreateSymbol($"{conversionDeclaration.ImplicitOrExplicitKeyword.Text} operator {conversionDeclaration.Type}", SymbolKind.Operator, conversionDeclaration));
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
            else if (node is BaseFieldDeclarationSyntax fieldDeclaration) {
                var kind = fieldDeclaration.Modifiers.Any(SyntaxKind.ConstKeyword) ? SymbolKind.Constant : SymbolKind.Field;
                if (fieldDeclaration is EventFieldDeclarationSyntax)
                    kind = SymbolKind.Event;

                foreach (var variable in fieldDeclaration.Declaration.Variables)
                    result.Add(CreateSymbol(variable.Identifier.Text, kind, fieldDeclaration));
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
            Name = GetFormattedName(memberDeclaration, name),
            Kind = kind,
            Range = range,
            SelectionRange = range,
            Children = includeChildren ? TraverseSyntaxTree(memberDeclaration.ChildNodes()) : null
        };
    }
}
