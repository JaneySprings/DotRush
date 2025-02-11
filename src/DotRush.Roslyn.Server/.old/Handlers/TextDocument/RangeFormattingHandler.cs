using DotRush.Roslyn.Server.Extensions;
using DotRush.Roslyn.Server.Services;
using DotRush.Roslyn.Workspaces.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Formatting;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace DotRush.Roslyn.Server.Handlers.TextDocument;

public class RangeFormattingHandler : DocumentRangeFormattingHandlerBase {
    private readonly WorkspaceService solutionService;

    public RangeFormattingHandler(WorkspaceService solutionService) {
        this.solutionService = solutionService;
    }

    protected override DocumentRangeFormattingRegistrationOptions CreateRegistrationOptions(DocumentRangeFormattingCapability capability, ClientCapabilities clientCapabilities) {
        return new DocumentRangeFormattingRegistrationOptions() {
            DocumentSelector = LanguageServer.SelectorForSourceCodeDocuments
        };
    }

    public override async Task<TextEditContainer> Handle(DocumentRangeFormattingParams request, CancellationToken cancellationToken) {
        var edits = new List<TextEdit>();
        var documentId = solutionService.Solution?.GetDocumentIdsWithFilePathV2(request.TextDocument.Uri.GetFileSystemPath()).FirstOrDefault();
        var document = solutionService.Solution?.GetDocument(documentId);
        if (document == null)
            return new TextEditContainer();

        var sourceText = await document.GetTextAsync(cancellationToken);
        var formattedDoc = await Formatter.FormatAsync(document, request.Range.ToTextSpan(sourceText), cancellationToken: cancellationToken);
        var textChanges = await formattedDoc.GetTextChangesAsync(document, cancellationToken);
        return new TextEditContainer(textChanges.Select(x => x.ToTextEdit(sourceText)));
    }
}