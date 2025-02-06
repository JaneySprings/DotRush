using DotRush.Roslyn.Server.Services;
using EmmyLua.LanguageServer.Framework.Protocol.Capabilities.Client.ClientCapabilities;
using EmmyLua.LanguageServer.Framework.Protocol.Capabilities.Server;
using EmmyLua.LanguageServer.Framework.Protocol.Capabilities.Server.Options;
using EmmyLua.LanguageServer.Framework.Protocol.Capabilities.Server.WorkspaceServerCapabilities;
using EmmyLua.LanguageServer.Framework.Protocol.Message.WorkspaceFiles;
using EmmyLua.LanguageServer.Framework.Protocol.Model;
using EmmyLua.LanguageServer.Framework.Protocol.Model.File;
using EmmyLua.LanguageServer.Framework.Server.Handler;

namespace DotRush.Roslyn.Server.Handlers.Workspace;

public class WorkspaceFilesHandler : WorkspaceFilesHandlerBase {
    private readonly WorkspaceService workspaceService;

    public WorkspaceFilesHandler(WorkspaceService workspaceService) {
        this.workspaceService = workspaceService;
    }

    public override void RegisterCapability(ServerCapabilities serverCapabilities, ClientCapabilities clientCapabilities) {
        var commonFilter = new FileOperationRegistrationOptions { Filters = new List<FileOperationFilter> {
            new FileOperationFilter { Pattern = new[] { new FileOperationPattern { Glob = "**/*.cs" }}},
            new FileOperationFilter { Pattern = new[] { new FileOperationPattern { Glob = "**/*.xaml" }}}
        }};
        serverCapabilities.Workspace ??= new WorkspaceServerCapabilities();
        serverCapabilities.Workspace.FileOperations = new FileOperationsServerCapabilities {
            DidCreate = commonFilter,
            DidRename = commonFilter,
            DidDelete = commonFilter
        };
    }

    protected override Task DidCreateFiles(CreateFilesParams request, CancellationToken token) {
        foreach (var file in request.Files)
            workspaceService.CreateDocument(file.Uri.FileSystemPath);
        
        return Task.CompletedTask;
    }
    protected override Task DidDeleteFiles(DeleteFilesParams request, CancellationToken token) {
        foreach (var file in request.Files)
            workspaceService.DeleteDocument(file.Uri.FileSystemPath);
        
        return Task.CompletedTask;
    }
    protected override Task DidRenameFiles(RenameFilesParams request, CancellationToken token) {
        foreach (var file in request.Files) {
            workspaceService.DeleteDocument(file.OldUri.FileSystemPath);
            workspaceService.CreateDocument(file.NewUri.FileSystemPath);
        }

        return Task.CompletedTask;
    }

    protected override Task<WorkspaceEdit?> WillCreateFiles(CreateFilesParams request, CancellationToken token) {
        return Task.FromResult<WorkspaceEdit?>(null);
    }
    protected override Task<WorkspaceEdit?> WillDeleteFiles(DeleteFilesParams request, CancellationToken token) {
        return Task.FromResult<WorkspaceEdit?>(null);
    }
    protected override Task<WorkspaceEdit?> WillRenameFiles(RenameFilesParams request, CancellationToken token) {
        return Task.FromResult<WorkspaceEdit?>(null);
    }
}