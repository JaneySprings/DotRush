using DotRush.Server.Extensions;
using DotRush.Server.Services;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace DotRush.Server.Handlers;

public class SignatureHelpHandler : SignatureHelpHandlerBase {
    private readonly SolutionService solutionService;


    public SignatureHelpHandler(SolutionService solutionService) {
        this.solutionService = solutionService;
    }

    protected override SignatureHelpRegistrationOptions CreateRegistrationOptions(SignatureHelpCapability capability, ClientCapabilities clientCapabilities) {
        return new SignatureHelpRegistrationOptions {
            DocumentSelector = DocumentSelector.ForLanguage("csharp"),
            TriggerCharacters = new Container<string>("(", ",")
        };
    }

    public override Task<SignatureHelp?> Handle(SignatureHelpParams request, CancellationToken cancellationToken) {
        return ServerExtensions.SafeHandlerAsync<SignatureHelp?>(async () => {
            // var documentIds = this.solutionService.Solution?.GetDocumentIdsWithFilePath(request.TextDocument.Uri.GetFileSystemPath());
            // if (documentIds == null)
            //      return null;

            // foreach (var documentId in documentIds) {
            //     var document = this.solutionService.Solution?.GetDocument(documentId);
            //     if (document == null)
            //         continue;

            //     var sourceText = await document.GetTextAsync(cancellationToken);
            //     var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
            //     var offset = request.Position.ToOffset(sourceText);
            //     var symbol = await SymbolFinder.FindSymbolAtPositionAsync(document, offset);
            //     if (symbol == null || semanticModel == null)
            //         continue;

            //     var displayString = symbol.Kind == CodeAnalysis.SymbolKind.NamedType 
            //         ? symbol.ToDisplayString() 
            //         : symbol.ToMinimalDisplayString(semanticModel, offset);

            //     return new SignatureHelp {
            //         Signatures = new Container<SignatureInformation>(new SignatureInformation {
            //             Label = displayString,
            //             Documentation = symbol.GetDocumentationCommentXml(),
            //             Parameters = new Container<ParameterInformation>(symbol.GetParameters().Select(p => new ParameterInformation {
            //                 Label = p.ToDisplayString()
            //             }))
            //         }),
            //         ActiveSignature = 0,
            //         ActiveParameter = 0
            //     };
            // }
            return null;
        });
    }
}