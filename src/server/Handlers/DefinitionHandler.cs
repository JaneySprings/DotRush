using DotRush.Server.Extensions;
using DotRush.Server.Services;
using DotRush.Server.Containers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using ProtocolModels = OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol;

namespace DotRush.Server.Handlers;

public class DefinitionHandler : DefinitionHandlerBase {
    private readonly SolutionService solutionService;
    private readonly DecompilationService decompilationService;

    public DefinitionHandler(SolutionService solutionService, DecompilationService decompilationService) {
        this.solutionService = solutionService;
        this.decompilationService = decompilationService;
    }

    protected override DefinitionRegistrationOptions CreateRegistrationOptions(DefinitionCapability capability, ClientCapabilities clientCapabilities) {
        return new DefinitionRegistrationOptions();
    }

    public override async Task<LocationOrLocationLinks> Handle(DefinitionParams request, CancellationToken cancellationToken) {
        var documentIds = this.solutionService.Solution?.GetDocumentIdsWithFilePath(request.TextDocument.Uri.GetFileSystemPath());
        if (documentIds == null)
            return new LocationOrLocationLinks();

        Document? document = null;
        ISymbol? symbol = null;

        var result = new LocationCollection();
        foreach (var documentId in documentIds) {
            document = this.solutionService.Solution?.GetDocument(documentId);
            if (document == null)
                continue;

            var sourceText = await document.GetTextAsync(cancellationToken);
            symbol = await SymbolFinder.FindSymbolAtPositionAsync(document, request.Position.ToOffset(sourceText), cancellationToken);
            if (symbol == null || symbol.Locations == null) 
                continue;

            result.AddRange(symbol.Locations.Select(loc => loc.ToLocation()));
        }

        if (result.IsEmpty && document != null && symbol != null) {
            var decompiledDocument = await this.decompilationService.DecompileAsync(symbol, document.Project, cancellationToken);
            //var model = await decompiledDocument.GetSemanticModelAsync();
            //var fullNameOfSymbol = "Microsoft.Maui.Controls.Shell";
            // find the symbol in the decompiled document
            //var symbolInDecompiledDocument = model.GetSymbolInfo(decompiledDocument.GetSyntaxRootAsync()!.DescendantNodes().First(node => node.ToString() == fullNameOfSymbol), cancellationToken).Symbol;
            
            // TODO
            result.Add(new ProtocolModels.Location() {
                Uri = DocumentUri.From(decompiledDocument!.FilePath!),
                Range = new ProtocolModels.Range() {
                    Start = new ProtocolModels.Position(0, 0),
                    End = new ProtocolModels.Position(0, 0),
                },
            });
        }
    
        return result.ToLocationOrLocationLinks();
    }
}