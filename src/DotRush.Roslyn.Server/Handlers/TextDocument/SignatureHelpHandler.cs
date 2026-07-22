using DotRush.Common.Extensions;
using DotRush.Roslyn.CodeAnalysis;
using DotRush.Roslyn.Server.Extensions;
using DotRush.Roslyn.Server.Services;
using DotRush.Roslyn.Workspaces.Extensions;
using EmmyLua.LanguageServer.Framework.Protocol.Capabilities.Client.ClientCapabilities;
using EmmyLua.LanguageServer.Framework.Protocol.Capabilities.Server;
using EmmyLua.LanguageServer.Framework.Protocol.Capabilities.Server.Options;
using EmmyLua.LanguageServer.Framework.Protocol.Message.SignatureHelp;
using EmmyLua.LanguageServer.Framework.Server.Handler;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DotRush.Roslyn.Server.Handlers.TextDocument;

public class SignatureHelpHandler : SignatureHelpHandlerBase {
    private readonly WorkspaceService solutionService;

    public SignatureHelpHandler(WorkspaceService solutionService) {
        this.solutionService = solutionService;
    }

    public override void RegisterCapability(ServerCapabilities serverCapabilities, ClientCapabilities clientCapabilities) {
        serverCapabilities.SignatureHelpProvider = new SignatureHelpOptions {
            TriggerCharacters = new List<string> { "(", "," }
        };
    }
    protected override Task<SignatureHelp> Handle(SignatureHelpParams request, CancellationToken token) {
        return SafeExtensions.InvokeAsync(new SignatureHelp(), async () => {
            var documentIds = solutionService.Solution?.GetDocumentIdsWithFilePathV2(request.TextDocument.Uri.FileSystemPath);
            if (documentIds == null)
                return new SignatureHelp();

            foreach (var documentId in documentIds) {
                var document = solutionService.Solution?.GetDocument(documentId);
                if (document == null)
                    continue;

                var sourceText = await document.GetTextAsync(token);
                var tree = await document.GetSyntaxTreeAsync(token);
                var semanticModel = await document.GetSemanticModelAsync(token);
                if (sourceText == null || tree == null || semanticModel == null)
                    continue;

                var position = request.Position.ToOffset(sourceText);
                var root = await tree.GetRootAsync(token);
                var node = root.FindToken(position).Parent;

                var invocationInfo = GetInvocationInfo(node, position);
                if (invocationInfo == null)
                    continue;

                var candidates = semanticModel
                    .GetMemberGroup(invocationInfo.MemberGroupNode, token)
                    .OfType<IMethodSymbol>()
                    .Where(x => CanAcceptArguments(x, invocationInfo.Arguments.Count))
                    .ToList();
                if (candidates.Count == 0)
                    continue;

                // The compiler binds valid invocations to the exact overload - prefer it over the score heuristic
                var boundSymbol = semanticModel.GetSymbolInfo(invocationInfo.InvocationNode, token).Symbol as IMethodSymbol;
                var argumentTypes = invocationInfo.Arguments
                    .Select(x => GetArgumentType(semanticModel, x, token))
                    .ToList();

                var activeSignature = 0;
                var bestScore = int.MinValue;
                var signatures = new List<SignatureInformation>(candidates.Count);
                for (var i = 0; i < candidates.Count; i++) {
                    var candidate = candidates[i];
                    signatures.Add(new SignatureInformation {
                        Label = candidate.ToDisplayString(DisplayFormat.Minimal),
                        Parameters = new List<ParameterInformation>(candidate.Parameters.Select(y => new ParameterInformation {
                            Label = y.ToDisplayString(DisplayFormat.Minimal)
                        })),
                        ActiveParameter = GetActiveParameter(candidate, invocationInfo),
                    });

                    var score = boundSymbol != null && SymbolEqualityComparer.Default.Equals(candidate.OriginalDefinition, boundSymbol.OriginalDefinition)
                        ? int.MaxValue
                        : GetSignatureScore(candidate, invocationInfo.Arguments, argumentTypes);
                    if (score > bestScore) {
                        bestScore = score;
                        activeSignature = i;
                    }
                }

                if (signatures.Count == 1 && signatures[0].Parameters.Count == 0)
                    continue;

                return new SignatureHelp {
                    Signatures = signatures,
                    ActiveSignature = (uint)activeSignature,
                    ActiveParameter = signatures[activeSignature].ActiveParameter ?? 0,
                };
            }

            return new SignatureHelp();
        });
    }

    private static InvocationInfo? GetInvocationInfo(SyntaxNode? node, int position) {
        while (node != null) {
            if (node is InvocationExpressionSyntax invocation && invocation.ArgumentList.Span.Contains(position))
                return new InvocationInfo(invocation.Expression, invocation, GetArguments(invocation.ArgumentList.Arguments), GetActiveArgument(invocation.ArgumentList.Arguments, position));
            if (node is BaseObjectCreationExpressionSyntax objectCreation && (objectCreation.ArgumentList?.Span.Contains(position) ?? false))
                return new InvocationInfo(objectCreation, objectCreation, GetArguments(objectCreation.ArgumentList.Arguments), GetActiveArgument(objectCreation.ArgumentList.Arguments, position));
            if (node is AttributeSyntax attributeSyntax && (attributeSyntax.ArgumentList?.Span.Contains(position) ?? false))
                return new InvocationInfo(attributeSyntax, attributeSyntax, GetAttributeArguments(attributeSyntax.ArgumentList.Arguments), GetActiveArgument(attributeSyntax.ArgumentList.Arguments, position));

            node = node.Parent;
        }
        return null;
    }
    private static List<ArgumentInfo> GetArguments(SeparatedSyntaxList<ArgumentSyntax> arguments) {
        return arguments.Select(x => new ArgumentInfo(x.Expression, x.NameColon?.Name.Identifier.ValueText)).ToList();
    }
    private static List<ArgumentInfo> GetAttributeArguments(SeparatedSyntaxList<AttributeArgumentSyntax> arguments) {
        // `Property = value` arguments don't map to constructor parameters
        return arguments.Select(x => new ArgumentInfo(x.NameEquals == null ? x.Expression : null, x.NameColon?.Name.Identifier.ValueText)).ToList();
    }
    private static int GetActiveArgument<TNode>(SeparatedSyntaxList<TNode> arguments, int position) where TNode : SyntaxNode {
        var index = 0;
        foreach (var separator in arguments.GetSeparators()) {
            if (separator.Span.End <= position)
                index++;
        }
        return index;
    }

    private static bool CanAcceptArguments(IMethodSymbol symbol, int argumentsCount) {
        if (symbol.Parameters.Length >= argumentsCount)
            return true;
        return symbol.Parameters.Length != 0 && symbol.Parameters[symbol.Parameters.Length - 1].IsParams;
    }
    private static ITypeSymbol? GetArgumentType(SemanticModel semanticModel, ArgumentInfo argument, CancellationToken cancellationToken) {
        if (argument.Expression == null || argument.Expression.IsMissing)
            return null;

        var convertedType = semanticModel.GetTypeInfo(argument.Expression, cancellationToken).ConvertedType;
        return convertedType == null || convertedType.TypeKind == TypeKind.Error ? null : convertedType;
    }
    private static int GetSignatureScore(IMethodSymbol symbol, List<ArgumentInfo> arguments, List<ITypeSymbol?> argumentTypes) {
        var score = 0;
        for (var i = 0; i < arguments.Count; i++) {
            var parameter = GetParameter(symbol, i, arguments[i].Name);
            if (parameter == null)
                continue;
            if (argumentTypes[i] == null)
                score += 1;
            else if (MatchesParameterType(argumentTypes[i]!, parameter))
                score += 2;
        }
        return score;
    }
    private static IParameterSymbol? GetParameter(IMethodSymbol symbol, int index, string? name) {
        if (name != null)
            return symbol.Parameters.FirstOrDefault(x => x.Name == name);
        if (index < symbol.Parameters.Length)
            return symbol.Parameters[index];

        var lastParameter = symbol.Parameters.LastOrDefault();
        return lastParameter?.IsParams == true ? lastParameter : null;
    }
    private static bool MatchesParameterType(ITypeSymbol argumentType, IParameterSymbol parameter) {
        if (SymbolEqualityComparer.Default.Equals(argumentType, parameter.Type))
            return true;
        // `params int[] values` also accepts `int` arguments
        return parameter.IsParams && parameter.Type is IArrayTypeSymbol arrayType
            && SymbolEqualityComparer.Default.Equals(argumentType, arrayType.ElementType);
    }
    private static uint? GetActiveParameter(IMethodSymbol symbol, InvocationInfo invocationInfo) {
        if (symbol.Parameters.Length == 0)
            return null;

        var argumentName = invocationInfo.ActiveArgument < invocationInfo.Arguments.Count
            ? invocationInfo.Arguments[invocationInfo.ActiveArgument].Name
            : null;
        if (argumentName != null) {
            for (var i = 0; i < symbol.Parameters.Length; i++) {
                if (symbol.Parameters[i].Name == argumentName)
                    return (uint)i;
            }
        }
        // Arguments beyond the parameter list can only be a `params` tail (see CanAcceptArguments)
        return (uint)Math.Min(invocationInfo.ActiveArgument, symbol.Parameters.Length - 1);
    }

    private sealed class ArgumentInfo {
        public ExpressionSyntax? Expression { get; }
        public string? Name { get; }

        public ArgumentInfo(ExpressionSyntax? expression, string? name) {
            Expression = expression;
            Name = name;
        }
    }
    private sealed class InvocationInfo {
        public SyntaxNode MemberGroupNode { get; }
        public SyntaxNode InvocationNode { get; }
        public List<ArgumentInfo> Arguments { get; }
        public int ActiveArgument { get; }

        public InvocationInfo(SyntaxNode memberGroupNode, SyntaxNode invocationNode, List<ArgumentInfo> arguments, int activeArgument) {
            MemberGroupNode = memberGroupNode;
            InvocationNode = invocationNode;
            Arguments = arguments;
            ActiveArgument = activeArgument;
        }
    }
}
