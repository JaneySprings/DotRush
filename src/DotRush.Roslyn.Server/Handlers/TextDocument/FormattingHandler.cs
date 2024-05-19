using DotRush.Roslyn.Server.Extensions;
using DotRush.Roslyn.Server.Services;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Formatting;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace DotRush.Roslyn.Server.Handlers.TextDocument;

public class FormattingHandler : DocumentFormattingHandlerBase {
    private readonly WorkspaceService solutionService;

    public FormattingHandler(WorkspaceService solutionService) {
        this.solutionService = solutionService;
    }

    protected override DocumentFormattingRegistrationOptions CreateRegistrationOptions(DocumentFormattingCapability capability, ClientCapabilities clientCapabilities) {
        return new DocumentFormattingRegistrationOptions() {
            DocumentSelector = LanguageServer.SelectorForSourceCodeDocuments
        };
    }

    public override async Task<TextEditContainer?> Handle(DocumentFormattingParams request, CancellationToken cancellationToken) {
        var edits = new List<TextEdit>();
        var documentId = solutionService.Solution?.GetDocumentIdsWithFilePath(request.TextDocument.Uri.GetFileSystemPath()).FirstOrDefault();
        var document = solutionService.Solution?.GetDocument(documentId);
        if (document == null)
            return null;

        var sourceText = await document.GetTextAsync(cancellationToken);
        var formattedDocument = await Formatter.FormatAsync(document, cancellationToken: cancellationToken);
        var textChanges = await formattedDocument.GetTextChangesAsync(document, cancellationToken);
        return new TextEditContainer(textChanges.Select(x => x.ToTextEdit(sourceText)));
    }
}