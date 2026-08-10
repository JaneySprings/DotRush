using DotRush.Common.Extensions;
using DotRush.Roslyn.CodeAnalysis.Extensions;
using DotRush.Roslyn.Server.Extensions;
using DotRush.Roslyn.Server.Services;
using DotRush.Roslyn.Workspaces.Extensions;
using EmmyLua.LanguageServer.Framework.Protocol.Capabilities.Client.ClientCapabilities;
using EmmyLua.LanguageServer.Framework.Protocol.Capabilities.Common;
using EmmyLua.LanguageServer.Framework.Protocol.Capabilities.Server;
using EmmyLua.LanguageServer.Framework.Protocol.Capabilities.Server.Options;
using EmmyLua.LanguageServer.Framework.Protocol.Message.SemanticToken;
using EmmyLua.LanguageServer.Framework.Server.Handler;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace DotRush.Roslyn.Server.Handlers.TextDocument;

public class SemanticTokensHandler : SemanticTokensHandlerBase {
    private readonly NavigationService navigationService;

    public SemanticTokensHandler(NavigationService navigationService) {
        this.navigationService = navigationService;
    }

    public override void RegisterCapability(ServerCapabilities serverCapabilities, ClientCapabilities clientCapabilities) {
        serverCapabilities.SemanticTokensProvider = new SemanticTokensOptions {
            Full = true,
            Range = true,
            Legend = new SemanticTokensLegend {
                TokenTypes = Enum.GetNames<SemanticTokenType>().Select(n => n.ToCamelCase()).ToList(),
                TokenModifiers = new List<string>(),
            }
        };
    }

    protected override Task<SemanticTokens?> Handle(SemanticTokensParams request, CancellationToken token) {
        return SafeExtensions.InvokeAsync(async () => {
            var documentPath = request.TextDocument.Uri.FileSystemPath;
            var documentId = navigationService?.Solution?.GetDocumentIdsWithFilePathV2(documentPath).FirstOrDefault();
            var document = navigationService?.Solution?.GetDocument(documentId);
            if (documentId == null || document == null)
                return null;

            var syntaxTree = await document.GetSyntaxTreeAsync(token);
            if (syntaxTree == null)
                return null;

            var root = await syntaxTree.GetRootAsync(token);
            if (root == null)
                return null;

            return await TraverseSyntaxTree(root.DescendantTokens(), document, token);
        });
    }
    protected override Task<SemanticTokens?> Handle(SemanticTokensRangeParams request, CancellationToken token) {
        return SafeExtensions.InvokeAsync(async () => {
            var documentPath = request.TextDocument.Uri.FileSystemPath;
            var documentId = navigationService?.Solution?.GetDocumentIdsWithFilePathV2(documentPath).FirstOrDefault();
            var document = navigationService?.Solution?.GetDocument(documentId);
            if (documentId == null || document == null)
                return null;

            var syntaxTree = await document.GetSyntaxTreeAsync(token);
            if (syntaxTree == null)
                return null;

            var sourceText = await document.GetTextAsync(token);
            var range = request.Range.ToTextSpan(sourceText);
            var root = await syntaxTree.GetRootAsync(token);
            if (root == null)
                return null;

            var nodes = root.DescendantTokens()
                .Where(node => node.FullSpan.IntersectsWith(range))
                .ToList();

            return await TraverseSyntaxTree(nodes, document, token);
        });
    }
    protected override Task<SemanticTokensDeltaResponse?> Handle(SemanticTokensDeltaParams semanticTokensDeltaParams, CancellationToken cancellationToken) {
        return Task.FromResult<SemanticTokensDeltaResponse?>(null);
    }

    private async Task<SemanticTokens?> TraverseSyntaxTree(IEnumerable<SyntaxToken> tokens, Document document, CancellationToken cancellationToken) {
        if (tokens == null || !tokens.Any())
            return null;

        var data = new List<uint>();
        var currentLine = 0;
        var currentCharacter = 0;
        void AddToken(int line, int column, int length, SemanticTokenType type) {
            int deltaLine = line - currentLine;
            int deltaColumn = line == currentLine ? column - currentCharacter : column;
            currentLine = line;
            currentCharacter = column;

            data.Add((uint)deltaLine);
            data.Add((uint)deltaColumn);
            data.Add((uint)length);
            data.Add((uint)type);
            data.Add(0);
        }

        var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
        var sourceText = await document.GetTextAsync(cancellationToken);
        foreach (var token in tokens) {
            foreach (var trivia in token.LeadingTrivia)
                ProcessToken(trivia, sourceText, AddToken);

            ProcessToken(token, semanticModel, AddToken);
        }

        return new SemanticTokens { Data = data };
    }
    private void ProcessToken(SyntaxToken token, SemanticModel? semanticModel, Action<int, int, int, SemanticTokenType> handler) {
        var type = GetTokenType(token, semanticModel);
        if (type == SemanticTokenType.Unknown)
            return;

        var lineSpan = token.GetLocation().GetLineSpan();
        var startLine = lineSpan.StartLinePosition.Line;
        var startCharacter = lineSpan.StartLinePosition.Character;
        var endCharacter = lineSpan.EndLinePosition.Character;
        var length = endCharacter - startCharacter;

        handler.Invoke(startLine, startCharacter, length, type);
    }
    private void ProcessToken(SyntaxTrivia trivia, SourceText sourceText, Action<int, int, int, SemanticTokenType> handler) {
        if (!trivia.IsKind(SyntaxKind.DisabledTextTrivia))
            return;

        var range = trivia.Span.ToRange(sourceText);
        for (int lineNumber = range.Start.Line; lineNumber <= range.End.Line; lineNumber++) {
            var startColumn = lineNumber == range.Start.Line ? range.Start.Character : 0;
            var endColumn = lineNumber == range.End.Line ? range.End.Character : sourceText.Lines[lineNumber].Span.Length;
            if (endColumn > startColumn)
                handler.Invoke(lineNumber, startColumn, endColumn - startColumn, SemanticTokenType.ExcludedCode);
        }
    }

    private SemanticTokenType GetTokenType(SyntaxToken token, SemanticModel? semanticModel) {
        if (token.IsKind(SyntaxKind.NumericLiteralToken))
            return SemanticTokenType.Number;
        if (token.IsStringExpression())
            return SemanticTokenType.String;
        if (token.IsControlKeyword())
            return SemanticTokenType.ControlKeyword;
        if (token.IsRegularKeyword())
            return SemanticTokenType.Keyword;
        // if (SyntaxFacts.IsPreprocessorKeyword(token.Kind()))
        //     return (uint)TokenTypes.IndexOf("macro");
        if (token.IsKind(SyntaxKind.IdentifierToken) && token.Parent.IsDeclaration() && semanticModel != null) {
            var symbol = semanticModel.GetDeclaredSymbol(token.Parent!);
            if (symbol != null)
                return symbol.ToSemanticTokenType();
        }
        if (token.IsKind(SyntaxKind.IdentifierToken) && token.Parent != null && semanticModel != null) {
            var symbol = semanticModel.GetSymbolInfo(token.Parent).Symbol;
            if (symbol != null)
                return symbol.ToSemanticTokenType();
        }

        return SemanticTokenType.Unknown;
        // return SemanticTokenType.Operator;
    }
}