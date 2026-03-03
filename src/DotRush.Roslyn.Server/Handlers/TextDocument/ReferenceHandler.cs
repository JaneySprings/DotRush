using DotRush.Common.Extensions;
using DotRush.Roslyn.CodeAnalysis.Reflection;
using DotRush.Roslyn.Server.Extensions;
using DotRush.Roslyn.Server.Services;
using DotRush.Roslyn.Workspaces.Extensions;
using EmmyLua.LanguageServer.Framework.Protocol.Capabilities.Client.ClientCapabilities;
using EmmyLua.LanguageServer.Framework.Protocol.Capabilities.Server;
using EmmyLua.LanguageServer.Framework.Protocol.Message.Reference;
using EmmyLua.LanguageServer.Framework.Protocol.Model;
using EmmyLua.LanguageServer.Framework.Server.Handler;
using Microsoft.CodeAnalysis.FindSymbols;

namespace DotRush.Roslyn.Server.Handlers.TextDocument;

public class ReferenceHandler : ReferenceHandlerBase {
    private readonly NavigationService navigationService;

    public ReferenceHandler(NavigationService navigationService) {
        this.navigationService = navigationService;
    }

    public override void RegisterCapability(ServerCapabilities serverCapabilities, ClientCapabilities clientCapabilities) {
        serverCapabilities.ReferencesProvider = true;
    }
    protected override Task<ReferenceResponse?> Handle(ReferenceParams request, CancellationToken cancellationToken) {
        return SafeExtensions.InvokeAsync<ReferenceResponse?>(async () => {
            var documentIds = navigationService.Solution?.GetDocumentIdsWithFilePathV2(request.TextDocument.Uri.FileSystemPath);
            if (documentIds == null || navigationService.Solution == null)
                return null;

            var result = new HashSet<Location>();
            foreach (var documentId in documentIds) {
                var document = navigationService.Solution.GetDocument(documentId);
                if (document == null)
                    continue;

                var sourceText = await document.GetTextAsync(cancellationToken);
                var symbol = await SymbolFinder.FindSymbolAtPositionAsync(document, request.Position.ToOffset(sourceText), cancellationToken);
                if (symbol == null)
                    continue;

                var referenceSymbols = await SymbolFinder.FindReferencesAsync(symbol, navigationService.Solution, cancellationToken);
                var referenceLocations = referenceSymbols
                    .SelectMany(r => r.Locations);

                if (symbol is Microsoft.CodeAnalysis.IMethodSymbol methodSymbol) {
                    if (methodSymbol.MethodKind == Microsoft.CodeAnalysis.MethodKind.PropertyGet)
                        referenceLocations = referenceLocations.Where(l => !InternalReferenceLocation.IsWrittenTo(l));
                    if (methodSymbol.MethodKind == Microsoft.CodeAnalysis.MethodKind.PropertySet)
                        referenceLocations = referenceLocations.Where(l => InternalReferenceLocation.IsWrittenTo(l));
                }

                foreach (var referenceLocation in referenceLocations) {
                    var location = referenceLocation.Location;
                    var filePath = location.SourceTree?.FilePath ?? string.Empty;
                    if (!File.Exists(filePath))
                        filePath = await navigationService.EmitCompilerGeneratedFileAsync(location, document.Project, cancellationToken).ConfigureAwait(false);

                    var serverLocation = location.ToLocation(filePath);
                    if (serverLocation != null)
                        result.Add(serverLocation.Value);
                }
            }

            return new ReferenceResponse(result.ToList());
        });
    }
}