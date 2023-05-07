using DotRush.Server.Extensions;
using DotRush.Server.Services;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Formatting;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace DotRush.Server.Handlers;

public class RangeFormattingHandler : DocumentRangeFormattingHandlerBase {
    private SolutionService solutionService;

    public RangeFormattingHandler(SolutionService solutionService) {
        this.solutionService = solutionService;
    }

    protected override DocumentRangeFormattingRegistrationOptions CreateRegistrationOptions(DocumentRangeFormattingCapability capability, ClientCapabilities clientCapabilities) {
        return new DocumentRangeFormattingRegistrationOptions();
    }

    public override async Task<TextEditContainer> Handle(DocumentRangeFormattingParams request, CancellationToken cancellationToken) {
        var edits = new List<TextEdit>();
        var document = this.solutionService.GetDocumentByPath(request.TextDocument.Uri.GetFileSystemPath());
        if (document == null) 
            return new TextEditContainer();

        var sourceText = await document.GetTextAsync(cancellationToken);
        var formattedDoc = await Formatter.FormatAsync(document, request.Range.ToTextSpan(sourceText), cancellationToken: cancellationToken);
        var textChanges = await formattedDoc.GetTextChangesAsync(document, cancellationToken);
        return new TextEditContainer(textChanges.Select(x => x.ToTextEdit(sourceText)));
    }
}