using DotRush.Common.Extensions;
using DotRush.Roslyn.CodeAnalysis;
using DotRush.Roslyn.CodeAnalysis.Reflection;
using DotRush.Roslyn.Server.Extensions;
using DotRush.Roslyn.Server.Services;
using DotRush.Roslyn.Workspaces.Extensions;
using EmmyLua.LanguageServer.Framework.Protocol.Capabilities.Client.ClientCapabilities;
using EmmyLua.LanguageServer.Framework.Protocol.Capabilities.Server;
using EmmyLua.LanguageServer.Framework.Protocol.Message.CallHierarchy;
using EmmyLua.LanguageServer.Framework.Protocol.Model;
using EmmyLua.LanguageServer.Framework.Server.Handler;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;

namespace DotRush.Roslyn.Server.Handlers.TextDocument;

public class CallHierarchyHandler : CallHierarchyHandlerBase {
    private readonly NavigationService navigationService;

    public CallHierarchyHandler(NavigationService navigationService) {
        this.navigationService = navigationService;
    }

    public override void RegisterCapability(ServerCapabilities serverCapabilities, ClientCapabilities clientCapabilities) {
        serverCapabilities.CallHierarchyProvider = true;
    }
    protected override Task<CallHierarchyPrepareResponse?> CallHierarchyPrepare(CallHierarchyPrepareParams request, CancellationToken token) {
        return SafeExtensions.InvokeAsync(async () => {
            var documentId = navigationService.Solution?.GetDocumentIdsWithFilePathV2(request.TextDocument.Uri.FileSystemPath).FirstOrDefault();
            if (documentId == null || navigationService.Solution == null)
                return null;

            var document = navigationService.Solution.GetDocument(documentId);
            if (document == null)
                return null;

            var sourceText = await document.GetTextAsync(token).ConfigureAwait(false);
            var symbol = await SymbolFinder.FindSymbolAtPositionAsync(document, request.Position.ToOffset(sourceText), token).ConfigureAwait(false);
            if (symbol is not IMethodSymbol && symbol is not IPropertySymbol && symbol is not IEventSymbol)
                return null;

            var item = CreateCallHierarchyItem(symbol, token);
            if (item == null)
                return null;

            return new CallHierarchyPrepareResponse(new List<CallHierarchyItem> { item });
        });
    }
    protected override Task<CallHierarchyIncomingCallsResponse> CallHierarchyIncomingCalls(CallHierarchyIncomingCallsParams request, CancellationToken token) {
        return SafeExtensions.InvokeAsync(new CallHierarchyIncomingCallsResponse(new List<CallHierarchyIncomingCall>()), async () => {
            var result = new List<CallHierarchyIncomingCall>();
            var symbol = await ResolveItemSymbolAsync(request.Item, token).ConfigureAwait(false);
            if (symbol == null || navigationService.Solution == null)
                return new CallHierarchyIncomingCallsResponse(result);

            var callers = await SymbolFinder.FindCallersAsync(symbol, navigationService.Solution, token).ConfigureAwait(false);
            var callersBySymbol = new Dictionary<string, (ISymbol symbol, HashSet<DocumentRange> fromRanges)>();
            foreach (var caller in callers) {
                if (!caller.IsDirect)
                    continue;

                var key = caller.CallingSymbol.ToDisplayString();
                if (!callersBySymbol.TryGetValue(key, out var incomingCall)) {
                    incomingCall = (caller.CallingSymbol, new HashSet<DocumentRange>());
                    callersBySymbol.Add(key, incomingCall);
                }
                foreach (var location in caller.Locations) {
                    if (location.SourceTree != null)
                        incomingCall.fromRanges.Add(location.ToRange());
                }
            }

            foreach (var (callingSymbol, fromRanges) in callersBySymbol.Values) {
                var fromItem = CreateCallHierarchyItem(callingSymbol, token);
                if (fromItem == null)
                    continue;

                result.Add(new CallHierarchyIncomingCall {
                    From = fromItem,
                    FromRanges = fromRanges.ToList()
                });
            }

            return new CallHierarchyIncomingCallsResponse(result);
        });
    }
    protected override Task<CallHierarchyOutgoingCallsResponse> CallHierarchyOutgoingCalls(CallHierarchyOutgoingCallsParams request, CancellationToken token) {
        return SafeExtensions.InvokeAsync(new CallHierarchyOutgoingCallsResponse(new List<CallHierarchyOutgoingCall>()), async () => {
            var result = new List<CallHierarchyOutgoingCall>();
            var symbol = await ResolveItemSymbolAsync(request.Item, token).ConfigureAwait(false);
            if (symbol == null || navigationService.Solution == null)
                return new CallHierarchyOutgoingCallsResponse(result);

            var calleesBySymbol = new Dictionary<string, (ISymbol symbol, HashSet<DocumentRange> fromRanges)>();
            foreach (var syntaxReference in symbol.DeclaringSyntaxReferences) {
                var syntaxNode = await syntaxReference.GetSyntaxAsync(token).ConfigureAwait(false);
                var document = navigationService.Solution.GetDocument(syntaxNode.SyntaxTree);
                if (document == null)
                    continue;

                var semanticModel = await document.GetSemanticModelAsync(token).ConfigureAwait(false);
                if (semanticModel == null)
                    continue;

                foreach (var node in syntaxNode.DescendantNodes()) {
                    if (node is not InvocationExpressionSyntax && node is not BaseObjectCreationExpressionSyntax)
                        continue;

                    var calleeSymbol = semanticModel.GetSymbolInfo(node, token).Symbol;
                    if (calleeSymbol is not IMethodSymbol)
                        continue;

                    var key = calleeSymbol.ToDisplayString();
                    if (!calleesBySymbol.TryGetValue(key, out var outgoingCall)) {
                        outgoingCall = (calleeSymbol, new HashSet<DocumentRange>());
                        calleesBySymbol.Add(key, outgoingCall);
                    }
                    outgoingCall.fromRanges.Add(node.GetLocation().ToRange());
                }
            }

            foreach (var (calleeSymbol, fromRanges) in calleesBySymbol.Values) {
                var toItem = CreateCallHierarchyItem(calleeSymbol, token);
                if (toItem == null)
                    continue;

                result.Add(new CallHierarchyOutgoingCall {
                    To = toItem,
                    FromRanges = fromRanges.ToList()
                });
            }

            return new CallHierarchyOutgoingCallsResponse(result);
        });
    }

    private async Task<ISymbol?> ResolveItemSymbolAsync(CallHierarchyItem item, CancellationToken token) {
        if (item.Data?.Value is not string symbolKey || navigationService.Solution == null)
            return null;

        var documentId = navigationService.Solution.GetDocumentIdsWithFilePathV2(item.Uri.FileSystemPath).FirstOrDefault();
        if (documentId == null)
            return null;

        var compilation = await navigationService.Solution.GetProject(documentId.ProjectId)!.GetCompilationAsync(token).ConfigureAwait(false);
        if (compilation == null)
            return null;

        return InternalSymbolKey.ResolveString(symbolKey, compilation, token);
    }
    private CallHierarchyItem? CreateCallHierarchyItem(ISymbol symbol, CancellationToken token) {
        var location = symbol.Locations.FirstOrDefault();
        var filePath = location?.SourceTree?.FilePath;
        if (location == null || filePath == null)
            return null;

        var symbolKey = InternalSymbolKey.CreateString(symbol, token);
        return new CallHierarchyItem {
            Name = symbol.ToDisplayString(DisplayFormat.Member),
            Kind = symbol.ToSymbolKind(),
            Detail = symbol.ToDisplayString(DisplayFormat.Default),
            Uri = filePath,
            Range = location.ToRange(),
            SelectionRange = location.ToRange(),
            Data = symbolKey != null ? (LSPAny)symbolKey : null
        };
    }
}
