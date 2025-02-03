using DotRush.Roslyn.Common.Extensions;
using DotRush.Roslyn.Server.Services;
using MediatR;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Workspace;

namespace DotRush.Roslyn.Server.Handlers.Workspace;

public class DidChangeWatchedFilesHandler : DidChangeWatchedFilesHandlerBase {
    private readonly WorkspaceService workspaceService;
    private readonly CodeAnalysisService codeAnalysisService;
    private bool shouldApplyWorkspaceChanges;

    public DidChangeWatchedFilesHandler(WorkspaceService workspaceService, CodeAnalysisService codeAnalysisService) {
        this.workspaceService = workspaceService;
        this.codeAnalysisService = codeAnalysisService;
    }

    protected override DidChangeWatchedFilesRegistrationOptions CreateRegistrationOptions(DidChangeWatchedFilesCapability capability, ClientCapabilities clientCapabilities) {
        return new DidChangeWatchedFilesRegistrationOptions() {
            Watchers = new[] {
                new OmniSharp.Extensions.LanguageServer.Protocol.Models.FileSystemWatcher() {
                    Kind = WatchKind.Create | WatchKind.Change | WatchKind.Delete,
                    GlobPattern = new GlobPattern("**/*")
                },
            }
        };
    }

    public override Task<Unit> Handle(DidChangeWatchedFilesParams request, CancellationToken cancellationToken) {
        if (workspaceService.Solution == null)
            return Unit.Task;

        shouldApplyWorkspaceChanges = false;
        foreach (var change in request.Changes) {
            var path = change.Uri.GetFileSystemPath();
            HandleFileChange(path, change.Type);

            if (change.Type == FileChangeType.Created && Directory.Exists(path)) {
                foreach (var filePath in FileSystemExtensions.GetVisibleFilesRecursive(path))
                    HandleFileChange(filePath, FileChangeType.Created);
            }
        }
        
        if (shouldApplyWorkspaceChanges)
            workspaceService.ApplyChanges();

        return Unit.Task;
    }

    private void HandleFileChange(string path, FileChangeType changeType) {
        if (LanguageExtensions.IsProjectFile(path) && changeType == FileChangeType.Changed)
            return; // Handled from IDE

        if (changeType == FileChangeType.Deleted) {
            workspaceService.DeleteDocument(path);
            workspaceService.DeleteFolder(path);
            codeAnalysisService.ResetClientDiagnostics(path);
            shouldApplyWorkspaceChanges = true;
            return;
        }

        if (changeType == FileChangeType.Changed) {
            workspaceService.UpdateDocument(path);
            return;
        }

        if (changeType == FileChangeType.Created && File.Exists(path)) {
            workspaceService.CreateDocument(path);
            shouldApplyWorkspaceChanges = true;
            return;
        }
    }
}