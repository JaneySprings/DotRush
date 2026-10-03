using DotRush.Protocol;
using DotRush.Protocol.Handlers;
using DotRush.Protocol.Models;
using DotRush.Roslyn.Server.Extensions;
using DotRush.Roslyn.Server.Services;
using DotRush.Roslyn.Workspaces.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Rename;
using ProtocolModels = DotRush.Protocol.Models;

namespace DotRush.Roslyn.Server.Handlers.TextDocument;

public class RenameHandler : RenameHandlerBase {
    private readonly WorkspaceService workspaceService;
    private readonly SymbolRenameOptions symbolRenameOptions = new SymbolRenameOptions(
        RenameOverloads: false,
        RenameInStrings: false,
        RenameInComments: false,
        RenameFile: false
    );

    public RenameHandler(WorkspaceService workspaceService) {
        this.workspaceService = workspaceService;
    }

    public override void RegisterCapability(ServerCapabilities serverCapabilities) {
        serverCapabilities.RenameProvider = new ProtocolModels.RenameOptions { PrepareProvider = true };
    }
    protected override async Task<DocumentRange?> Handle(PrepareRenameParams request, CancellationToken token) {
        var documentId = workspaceService.Solution?.GetDocumentIdsWithFilePathV2(request.TextDocument.Uri.FileSystemPath).FirstOrDefault();
        var document = workspaceService.Solution?.GetDocument(documentId);
        if (document == null)
            return null;

        var sourceText = await document.GetTextAsync(token).ConfigureAwait(false);
        var offset = request.Position.ToOffset(sourceText);
        var symbol = await SymbolFinder.FindSymbolAtPositionAsync(document, offset, token).ConfigureAwait(false);
        if (symbol == null || !symbol.Locations.Any(x => x.IsInSource))
            return null;

        var root = await document.GetSyntaxRootAsync(token).ConfigureAwait(false);
        return root?.FindToken(offset).Span.ToRange(sourceText);
    }
    protected override async Task<WorkspaceEdit?> Handle(RenameParams request, CancellationToken token) {
        if (!SyntaxFacts.IsValidIdentifier(request.NewName.TrimStart('@')))
            throw new ProtocolException(string.Format(null, Resources.RenameInvalidIdentifierCompositeFormat, request.NewName));

        var workspaceEdits = new Dictionary<DocumentUri, List<TextEdit>>();
        var documentIds = workspaceService.Solution?.GetDocumentIdsWithFilePathV2(request.TextDocument.Uri.FileSystemPath);
        if (documentIds == null)
            return null;

        foreach (var documentId in documentIds) {
            var document = workspaceService.Solution?.GetDocument(documentId);
            if (document == null)
                continue;

            var sourceText = await document.GetTextAsync(token).ConfigureAwait(false);
            var symbol = await SymbolFinder.FindSymbolAtPositionAsync(document, request.Position.ToOffset(sourceText), token).ConfigureAwait(false);
            if (symbol == null)
                continue;

            var updatedSolution = await Renamer.RenameSymbolAsync(document.Project.Solution, symbol, symbolRenameOptions, request.NewName, token).ConfigureAwait(false);
            var changes = updatedSolution.GetChanges(document.Project.Solution);
            foreach (var change in changes.GetProjectChanges()) {
                if (change.NewProject.FilePath == null || change.OldProject.FilePath == null)
                    continue;

                foreach (var changedDocId in change.GetChangedDocuments()) {
                    var newDocument = change.NewProject.GetDocument(changedDocId);
                    var oldDocument = change.OldProject.GetDocument(changedDocId);
                    if (newDocument?.FilePath == null || oldDocument?.FilePath == null)
                        continue;

                    var oldSourceText = await oldDocument.GetTextAsync(token).ConfigureAwait(false);
                    var textChanges = await newDocument.GetTextChangesAsync(oldDocument, token).ConfigureAwait(false);
                    var textEdits = textChanges.Select(x => x.ToTextEdit(oldSourceText));
                    if (!textEdits.Any())
                        continue;

                    if (!workspaceEdits.ContainsKey(newDocument.FilePath))
                        workspaceEdits.Add(newDocument.FilePath, new List<TextEdit>());

                    foreach (var textEdit in textEdits) {
                        if (!workspaceEdits[newDocument.FilePath].Any(x => x.Range == textEdit.Range))
                            workspaceEdits[newDocument.FilePath].Add(textEdit);
                    }
                }
            }
        }

        if (workspaceEdits.Count == 0)
            return null;

        return new WorkspaceEdit() { Changes = workspaceEdits };
    }
}