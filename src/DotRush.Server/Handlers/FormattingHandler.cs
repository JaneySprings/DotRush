using DotRush.Server.Extensions;
using DotRush.Server.Services;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Formatting;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace DotRush.Server.Handlers;

public class FormattingHandler : DocumentFormattingHandlerBase {
    private SolutionService solutionService;

    public FormattingHandler(SolutionService solutionService) {
        this.solutionService = solutionService;
    }

    protected override DocumentFormattingRegistrationOptions CreateRegistrationOptions(DocumentFormattingCapability capability, ClientCapabilities clientCapabilities) {
        return new DocumentFormattingRegistrationOptions();
    }

    public override async Task<TextEditContainer?> Handle(DocumentFormattingParams request, CancellationToken cancellationToken) {
        var edits = new List<TextEdit>();
        var document = this.solutionService.GetDocumentByPath(request.TextDocument.Uri.GetFileSystemPath());
        if (document == null) 
            return null;

        var formattedDoc = await Formatter.FormatAsync(document, cancellationToken: cancellationToken);
        var textChanges = await formattedDoc.GetTextChangesAsync(document, cancellationToken);
        return new TextEditContainer(textChanges.Select(x => x.ToTextEdit(document)));
    }
}