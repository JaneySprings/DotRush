using DotRush.Roslyn.Common.Extensions;
using DotRush.Roslyn.Server.Extensions;
using DotRush.Roslyn.Server.Services;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace DotRush.Roslyn.Server.Handlers;

public class SignatureHelpHandler : SignatureHelpHandlerBase {
    private readonly WorkspaceService solutionService;

    public SignatureHelpHandler(WorkspaceService solutionService) {
        this.solutionService = solutionService;
    }

    protected override SignatureHelpRegistrationOptions CreateRegistrationOptions(SignatureHelpCapability capability, ClientCapabilities clientCapabilities) {
        return new SignatureHelpRegistrationOptions {
            DocumentSelector = LanguageServer.SelectorForSourceCodeDocuments,
            TriggerCharacters = new Container<string>("(", ",")
        };
    }

    public override Task<SignatureHelp?> Handle(SignatureHelpParams request, CancellationToken cancellationToken) {
        return SafeExtensions.InvokeAsync<SignatureHelp?>(async () => {
            var documentIds = this.solutionService.Solution?.GetDocumentIdsWithFilePath(request.TextDocument.Uri.GetFileSystemPath());
            if (documentIds == null)
                 return null;

            foreach (var documentId in documentIds) {
                var document = this.solutionService.Solution?.GetDocument(documentId);
                if (document == null)
                    continue;

                var sourceText = await document.GetTextAsync(cancellationToken);
                var tree = await document.GetSyntaxTreeAsync(cancellationToken);
                var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
                if (sourceText == null || tree == null || semanticModel == null)
                    continue;

                var position = request.Position.ToOffset(sourceText);
                var root = await tree.GetRootAsync();
                var node = root.FindToken(position).Parent;

                var invocationInfo = GetInvokationInfo(node, semanticModel, position, cancellationToken);
                if (invocationInfo == null)
                    continue;
                
                var (invocation, argumentsCount) = invocationInfo.Value;
                var signatures = new Container<SignatureInformation>(semanticModel
                    .GetMemberGroup(invocation, cancellationToken)
                    .OfType<IMethodSymbol>()
                    .Where(x => x.Parameters.Length >= argumentsCount)
                    .Select(x => new SignatureInformation { 
                        Label = x.ToDisplayString(HoverHandler.MinimalFormat),
                        Parameters = new Container<ParameterInformation>(x.Parameters.Select(y => new ParameterInformation { 
                            Label = y.ToDisplayString(HoverHandler.MinimalFormat)
                        }))
                    })
                );

                if (signatures.Count() == 1 && signatures.First().Parameters?.Count() == 0)
                    return null;

                return new SignatureHelp {
                    ActiveParameter = argumentsCount - 1,
                    Signatures = signatures
                };
            }

            return null;
        });
    }

    private (SyntaxNode, int)? GetInvokationInfo(SyntaxNode? node, SemanticModel semanticModel, int position, CancellationToken cancellationToken) {
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