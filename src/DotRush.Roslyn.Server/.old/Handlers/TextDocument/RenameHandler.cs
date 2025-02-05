using DotRush.Roslyn.Server.Extensions;
using DotRush.Roslyn.Server.Services;
using DotRush.Roslyn.Workspaces.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Rename;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace DotRush.Roslyn.Server.Handlers.TextDocument;

public class RenameHandler : RenameHandlerBase {
    private readonly WorkspaceService workspaceService;
    private readonly SymbolRenameOptions RenameOptions = new SymbolRenameOptions(
        RenameOverloads: false,
        RenameInStrings: false,
        RenameInComments: false,
        RenameFile: false
    );

    public RenameHandler(WorkspaceService workspaceService) {
        this.workspaceService = workspaceService;
    }

    protected override RenameRegistrationOptions CreateRegistrationOptions(RenameCapability capability, ClientCapabilities clientCapabilities) {
        return new RenameRegistrationOptions() {
            DocumentSelector = LanguageServer.SelectorForSourceCodeDocuments
        };
    }
    public override async Task<WorkspaceEdit?> Handle(RenameParams request, CancellationToken cancellationToken) {
        var workspaceEdits = new Dictionary<string, HashSet<TextEdit>>();
        var documentIds = workspaceService.Solution?.GetDocumentIdsWithFilePathV2(request.TextDocument.Uri.GetFileSystemPath());
        if (documentIds == null)
            return null;

        foreach (var documentId in documentIds) {
            var document = workspaceService.Solution?.GetDocument(documentId);
            if (document == null)
                continue;

            var sourceText = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
            var symbol = await SymbolFinder.FindSymbolAtPositionAsync(document, request.Position.ToOffset(sourceText), cancellationToken).ConfigureAwait(false);
            if (symbol == null)
                continue;

            var updatedSolution = await Renamer.RenameSymbolAsync(document.Project.Solution, symbol, RenameOptions, request.NewName, cancellationToken).ConfigureAwait(false);
            var changes = updatedSolution.GetChanges(document.Project.Solution);
            foreach (var change in changes.GetProjectChanges()) {
                if (change.NewProject.FilePath == null || change.OldProject.FilePath == null)
                    continue;

                foreach (var changedDocId in change.GetChangedDocuments()) {
                    var newDocument = change.NewProject.GetDocument(changedDocId);
                    var oldDocument = change.OldProject.GetDocument(changedDocId);
                    if (newDocument?.FilePath == null || oldDocument?.FilePath == null)
                        continue;

                    var oldSourceText = await oldDocument.GetTextAsync(cancellationToken).ConfigureAwait(false);
                    var textChanges = await newDocument.GetTextChangesAsync(oldDocument, cancellationToken).ConfigureAwait(false);
                    var textEdits = textChanges.Select(x => x.ToTextEdit(oldSourceText));
                    if (!textEdits.Any())
                        continue;

                    if (!workspaceEdits.ContainsKey(newDocument.FilePath))
                        workspaceEdits.Add(newDocument.FilePath, new HashSet<TextEdit>());

                    workspaceEdits[newDocument.FilePath].UnionWith(textEdits);
                }
            }
        }

        return new WorkspaceEdit() {
            Changes = workspaceEdits.ToDictionary(
                it => DocumentUri.FromFileSystemPath(it.Key),
                it => (IEnumerable<TextEdit>)it.Value
            )
        };
    }
}
