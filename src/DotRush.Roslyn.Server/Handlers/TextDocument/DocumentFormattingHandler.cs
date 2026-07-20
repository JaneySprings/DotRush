using DotRush.Roslyn.Server.Extensions;
using DotRush.Roslyn.Server.Services;
using DotRush.Roslyn.Workspaces.Extensions;
using EmmyLua.LanguageServer.Framework.Protocol.Capabilities.Client.ClientCapabilities;
using EmmyLua.LanguageServer.Framework.Protocol.Capabilities.Server;
using EmmyLua.LanguageServer.Framework.Protocol.Message.DocumentFormatting;
using EmmyLua.LanguageServer.Framework.Protocol.Model.TextEdit;
using EmmyLua.LanguageServer.Framework.Server.Handler;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Formatting;

namespace DotRush.Roslyn.Server.Handlers.TextDocument;

public class DocumentFormattingHandler : DocumentFormattingHandlerBase {
    private readonly WorkspaceService solutionService;

    public DocumentFormattingHandler(WorkspaceService solutionService) {
        this.solutionService = solutionService;
    }

    public override void RegisterCapability(ServerCapabilities serverCapabilities, ClientCapabilities clientCapabilities) {
        serverCapabilities.DocumentFormattingProvider = true;
        serverCapabilities.DocumentRangeFormattingProvider = true;
    }
    protected override async Task<DocumentFormattingResponse?> Handle(DocumentFormattingParams request, CancellationToken token) {
        var documentIds = solutionService.Solution?.GetDocumentIdsWithFilePathV2(request.TextDocument.Uri.FileSystemPath);
        if (documentIds == null)
            return null;

        var edits = new HashSet<TextEdit>(TextEditEqualityComparer.Default);
        foreach (var documentId in documentIds) {
            var document = solutionService.Solution?.GetDocument(documentId);
            if (document == null)
                return null;

            var sourceText = await document.GetTextAsync(token);
            var optionSet = await request.Options.ToOptionSetAsync(document, token);
            var formattedDocument = await Formatter.FormatAsync(document, optionSet, cancellationToken: token);
            formattedDocument = await Formatter.OrganizeImportsAsync(formattedDocument, token);

            var textChanges = await formattedDocument.GetTextChangesAsync(document, token);
            foreach (var textEdit in textChanges.Select(x => x.ToTextEdit(sourceText))) {
                if (!edits.Any(x => PositionExtensions.CheckCollision(x.Range, textEdit.Range)))
                    edits.Add(textEdit);
            }
        }

        return new DocumentFormattingResponse(edits.ToList());
    }
    protected override async Task<DocumentFormattingResponse?> Handle(DocumentRangesFormattingParams request, CancellationToken token) {
        var documentIds = solutionService.Solution?.GetDocumentIdsWithFilePathV2(request.TextDocument.Uri.FileSystemPath);
        if (documentIds == null)
            return null;

        var edits = new HashSet<TextEdit>(TextEditEqualityComparer.Default);
        foreach (var documentId in documentIds) {
            var document = solutionService.Solution?.GetDocument(documentId);
            if (document == null)
                return null;

            var sourceText = await document.GetTextAsync(token);
            var spans = request.Ranges.Select(range => range.ToTextSpan(sourceText)).ToArray();
            var optionSet = await request.Options.ToOptionSetAsync(document, token);
            var formattedDocument = await Formatter.FormatAsync(document, spans, optionSet, cancellationToken: token);
            var textChanges = await formattedDocument.GetTextChangesAsync(document, token);
            foreach (var textEdit in textChanges.Select(x => x.ToTextEdit(sourceText))) {
                if (!edits.Any(x => PositionExtensions.CheckCollision(x.Range, textEdit.Range)))
                    edits.Add(textEdit);
            }
        }

        return new DocumentFormattingResponse(edits.ToList());
    }
    protected override Task<DocumentFormattingResponse?> Handle(DocumentRangeFormattingParams request, CancellationToken token) {
        return Handle(request.ToRangesParameters(), token);
    }
    protected override Task<DocumentFormattingResponse?> Handle(DocumentOnTypeFormattingParams request, CancellationToken token) {
        return Task.FromResult<DocumentFormattingResponse?>(null);
    }
}