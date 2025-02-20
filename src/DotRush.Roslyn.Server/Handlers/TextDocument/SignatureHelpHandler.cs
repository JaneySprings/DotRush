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
                var root = await tree.GetRootAsync();
                var node = root.FindToken(position).Parent;

                var invocationInfo = GetInvokationInfo(node, semanticModel, position, token);
                if (invocationInfo == null)
                    continue;

                var (invocation, argumentsCount) = invocationInfo.Value;
                var signatures = new List<SignatureInformation>(semanticModel
                    .GetMemberGroup(invocation, token)
                    .OfType<IMethodSymbol>()
                    .Where(x => x.Parameters.Length >= argumentsCount)
                    .Select(x => new SignatureInformation {
                        Label = x.ToDisplayString(DisplayFormat.Minimal),
                        Parameters = new List<ParameterInformation>(x.Parameters.Select(y => new ParameterInformation {
                            Label = y.ToDisplayString(DisplayFormat.Minimal)
                        }))
                    })
                );

                if (signatures.Count == 1 && signatures.First().Parameters.Count == 0)
                    return new SignatureHelp();

                return new SignatureHelp {
                    ActiveParameter = (uint)argumentsCount - 1,
                    Signatures = signatures
                };
            }

            return new SignatureHelp();
        });
    }

    private static (SyntaxNode, int)? GetInvokationInfo(SyntaxNode? node, SemanticModel semanticModel, int position, CancellationToken cancellationToken) {
        while (node != null) {
            if (node is InvocationExpressionSyntax invocation && invocation.ArgumentList.Span.Contains(position))
                return (invocation.Expression, invocation.ArgumentList.Arguments.Count);
            if (node is BaseObjectCreationExpressionSyntax objectCreation && (objectCreation.ArgumentList?.Span.Contains(position) ?? false))
                return (objectCreation, objectCreation.ArgumentList.Arguments.Count);
            if (node is AttributeSyntax attributeSyntax && (attributeSyntax.ArgumentList?.Span.Contains(position) ?? false))
                return (attributeSyntax, attributeSyntax.ArgumentList.Arguments.Count);

            node = node.Parent;
        }
        return null;
    }
}