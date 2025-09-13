using DotRush.Common.Extensions;
using DotRush.Roslyn.CodeAnalysis.Reflection;
using DotRush.Roslyn.Server.Extensions;
using DotRush.Roslyn.Server.Services;
using DotRush.Roslyn.Workspaces.Extensions;
using EmmyLua.LanguageServer.Framework.Protocol.Capabilities.Client.ClientCapabilities;
using EmmyLua.LanguageServer.Framework.Protocol.Capabilities.Server;
using EmmyLua.LanguageServer.Framework.Protocol.Message.InlayHint;
using EmmyLua.LanguageServer.Framework.Protocol.Model.TextEdit;
using EmmyLua.LanguageServer.Framework.Server.Handler;

namespace DotRush.Roslyn.Server.Handlers.TextDocument;

public class InlayHintHandler : InlayHintHandlerBase {
    private readonly WorkspaceService workspaceService;
    private object? inlineHintsService;

    public InlayHintHandler(WorkspaceService workspaceService) {
        this.workspaceService = workspaceService;
    }

    public override void RegisterCapability(ServerCapabilities serverCapabilities, ClientCapabilities clientCapabilities) {
        serverCapabilities.InlayHintProvider = true;
    }

    protected override Task<InlayHintResponse?> Handle(InlayHintParams request, CancellationToken cancellationToken) {
        return SafeExtensions.InvokeAsync<InlayHintResponse?>(async () => {
            var documentIds = workspaceService.Solution?.GetDocumentIdsWithFilePathV2(request.TextDocument.Uri.FileSystemPath);
            if (documentIds == null)
                return null;

            if (inlineHintsService == null)
                inlineHintsService = InternalCSharpInlineHintsService.CreateNew();

            var hints = new HashSet<InlayHint>(InlayHintEqualityComparer.Default);
            foreach (var documentId in documentIds) {
                var document = workspaceService.Solution?.GetDocument(documentId);
                if (document == null)
                    continue;

                var sourceText = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
                var requestRange = request.Range.ToTextSpan(sourceText);
                var result = await InternalCSharpInlineHintsService.GetInlineHintsAsync(inlineHintsService, document, requestRange, InternalInlineHintsOptions.Default, true, cancellationToken).ConfigureAwait(false);
                if (result == null)
                    continue;

                foreach (var item in result) {
                    var textChange = InternalInlineHint.GetReplacementTextChange(item)?.ToTextEdit(sourceText);
                    var displayParts = InternalInlineHint.GetDisplayParts(item);
                    var inlineHint = new InlayHint();

                    inlineHint.Position = InternalInlineHint.GetSpan(item).ToRange(sourceText).Start;
                    inlineHint.Label = string.Concat(displayParts);
                    if (textChange != null)
                        inlineHint.TextEdits = new List<TextEdit> { textChange };
                    if (displayParts.Length == 0)
                        inlineHint.Label = "?";
                    inlineHint.PaddingRight = inlineHint.Label.String?.Last() != ' ';

                    hints.Add(inlineHint);
                }
            }

            return new InlayHintResponse(hints.ToList());
        });
    }
    protected override Task<InlayHint> Resolve(InlayHint request, CancellationToken cancellationToken) {
        return Task.FromResult(request);
    }

    class InlayHintEqualityComparer : IEqualityComparer<InlayHint> {
        public static InlayHintEqualityComparer Default { get; } = new InlayHintEqualityComparer();

        public bool Equals(InlayHint? x, InlayHint? y) {
            if (x == null && y == null)
                return true;
            if (x == null || y == null)
                return false;

            return GetHashCode(x) == GetHashCode(y);
        }
        public int GetHashCode(InlayHint obj) {
            return HashCode.Combine(obj.Position, obj.Label.String);
        }
    }
}