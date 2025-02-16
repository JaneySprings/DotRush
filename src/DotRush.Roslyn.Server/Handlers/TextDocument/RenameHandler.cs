using DotRush.Roslyn.Server.Extensions;
using DotRush.Roslyn.Server.Services;
using DotRush.Roslyn.Workspaces.Extensions;
using EmmyLua.LanguageServer.Framework.Protocol.Capabilities.Client.ClientCapabilities;
using EmmyLua.LanguageServer.Framework.Protocol.Capabilities.Server;
using EmmyLua.LanguageServer.Framework.Protocol.Message.Rename;
using EmmyLua.LanguageServer.Framework.Protocol.Model;
using EmmyLua.LanguageServer.Framework.Protocol.Model.TextEdit;
using EmmyLua.LanguageServer.Framework.Server.Handler;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Rename;

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

    public override void RegisterCapability(ServerCapabilities serverCapabilities, ClientCapabilities clientCapabilities) {
        serverCapabilities.RenameProvider = true;
    }
    protected override Task<PrepareRenameResponse> Handle(PrepareRenameParams request, CancellationToken token) {
        return Task.FromResult(new PrepareRenameResponse(false));
    }
    protected override async Task<WorkspaceEdit?> Handle(RenameParams request, CancellationToken token) {
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

            var updatedSolution = await Renamer.RenameSymbolAsync(document.Project.Solution, symbol, RenameOptions, request.NewName, token).ConfigureAwait(false);
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
                        if (!workspaceEdits[newDocument.FilePath].Any(x => x.Range.OverlapsWith(textEdit.Range)))
                            workspaceEdits[newDocument.FilePath].Add(textEdit);
                    }
                }
            }
        }

        return new WorkspaceEdit() { Changes = workspaceEdits };
    }
}