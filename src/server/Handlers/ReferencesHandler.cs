using DotRush.Server.Extensions;
using DotRush.Server.Services;
using Microsoft.CodeAnalysis.FindSymbols;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace DotRush.Server.Handlers;

public class ReferencesHandler : ReferencesHandlerBase {
    private SolutionService solutionService;

    public ReferencesHandler(SolutionService solutionService) {
        this.solutionService = solutionService;
    }

    protected override ReferenceRegistrationOptions CreateRegistrationOptions(ReferenceCapability capability, ClientCapabilities clientCapabilities) {
        return new ReferenceRegistrationOptions();
    }

    public override async Task<LocationContainer> Handle(ReferenceParams request, CancellationToken cancellationToken) {
        var documentId = this.solutionService.Solution?.GetDocumentIdsWithFilePath(request.TextDocument.Uri.GetFileSystemPath()).FirstOrDefault();
        var document = this.solutionService.Solution?.GetDocument(documentId);
        if (document == null)
            return new LocationContainer();

        var sourceText = await document.GetTextAsync(cancellationToken);
        var symbol = await SymbolFinder.FindSymbolAtPositionAsync(document, request.Position.ToOffset(sourceText), cancellationToken);
        if (symbol == null || this.solutionService.Solution == null) 
            return new LocationContainer();

        var referenceSymbols = await SymbolFinder.FindReferencesAsync(symbol, this.solutionService.Solution, cancellationToken);
        var referenceLocations = referenceSymbols
            .SelectMany(r => r.Locations)
            .Where(l => File.Exists(l.Document.FilePath));

        var result = new LocationCollection();
        foreach (var location in referenceLocations) {
            var locationSourceText = await location.Document.GetTextAsync(cancellationToken);
            var referenceLocation = location.ToLocation(locationSourceText);
            if (referenceLocation != null)
                result.Add(referenceLocation);
        }

        return result.ToLocationContainer();
    }
}