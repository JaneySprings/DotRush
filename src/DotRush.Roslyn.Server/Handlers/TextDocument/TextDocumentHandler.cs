using System.Collections.Concurrent;
using DotRush.Roslyn.Server.Services;
using DotRush.Roslyn.Workspaces.Extensions;
using EmmyLua.LanguageServer.Framework.Protocol.Capabilities.Client.ClientCapabilities;
using EmmyLua.LanguageServer.Framework.Protocol.Capabilities.Server;
using EmmyLua.LanguageServer.Framework.Protocol.Capabilities.Server.Options;
using EmmyLua.LanguageServer.Framework.Protocol.Message.TextDocument;
using EmmyLua.LanguageServer.Framework.Protocol.Model.TextEdit;
using EmmyLua.LanguageServer.Framework.Server.Handler;
using Microsoft.CodeAnalysis;

namespace DotRush.Roslyn.Server.Handlers.TextDocument;

public class TextDocumentHandler : TextDocumentHandlerBase {
    private readonly WorkspaceService workspaceService;
    private readonly CodeAnalysisService codeAnalysisService;
    private readonly ConcurrentDictionary<string, byte> openDocuments = [];

    public TextDocumentHandler(WorkspaceService workspaceService, CodeAnalysisService codeAnalysisService) {
        this.workspaceService = workspaceService;
        this.codeAnalysisService = codeAnalysisService;
    }

    public override void RegisterCapability(ServerCapabilities serverCapabilities, ClientCapabilities clientCapabilities) {
        serverCapabilities.TextDocumentSync = new TextDocumentSyncOptions {
            Change = TextDocumentSyncKind.Full,
            OpenClose = true,
        };
    }

    protected override Task Handle(DidOpenTextDocumentParams request, CancellationToken token) {
        var filePath = request.TextDocument.Uri.FileSystemPath;
        openDocuments[filePath] = 0;
        codeAnalysisService.RequestDiagnosticsPublishing(GetDocumentsWithFilePath(filePath), CancellationToken.None);
        return Task.CompletedTask;
    }
    protected override Task Handle(DidChangeTextDocumentParams request, CancellationToken token) {
        var filePath = request.TextDocument.Uri.FileSystemPath;
        var text = request.ContentChanges.First().Text;

        workspaceService.UpdateDocument(filePath, text);
        codeAnalysisService.RequestDiagnosticsPublishing(GetAllOpenDocuments(), CancellationToken.None);
        return Task.CompletedTask;
    }
    protected override Task Handle(DidCloseTextDocumentParams request, CancellationToken token) {
        var filePath = request.TextDocument.Uri.FileSystemPath;
        openDocuments.TryRemove(filePath, out _);
        return Task.CompletedTask;
    }
    protected override Task Handle(WillSaveTextDocumentParams request, CancellationToken token) {
        return Task.CompletedTask;
    }
    protected override Task<List<TextEdit>?> HandleRequest(WillSaveTextDocumentParams request, CancellationToken token) {
        return Task.FromResult<List<TextEdit>?>(null);
    }

    private List<Document> GetAllOpenDocuments() {
        var documents = new List<Document>();
        foreach (var filePath in openDocuments.Keys) {
            documents.AddRange(GetDocumentsWithFilePath(filePath));
        }
        return documents;
    }

    private List<Document> GetDocumentsWithFilePath(string filePath) {
        var documentIds = workspaceService.Solution?.GetDocumentIdsWithFilePathV2(filePath);
        if (documentIds == null || workspaceService.Solution == null)
            return [];

        var documentIdsList = documentIds.ToList();
        if (documentIdsList.Count == 0)
            return [];

        var documents = new List<Document>(documentIdsList.Count);
        foreach (var documentId in documentIdsList) {
            var document = workspaceService.Solution.GetDocument(documentId);
            if (document != null)
                documents.Add(document);
        }

        return documents;
    }
}