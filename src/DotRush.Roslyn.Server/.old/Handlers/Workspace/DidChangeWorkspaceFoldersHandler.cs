using DotRush.Roslyn.Server.Services;
using MediatR;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Workspace;

namespace DotRush.Roslyn.Server.Handlers.Workspace;

public class DidChangeWorkspaceFoldersHandler : DidChangeWorkspaceFoldersHandlerBase {
    private readonly WorkspaceService workspaceService;

    public DidChangeWorkspaceFoldersHandler(WorkspaceService workspaceService) {
        this.workspaceService = workspaceService;
    }

    protected override DidChangeWorkspaceFolderRegistrationOptions CreateRegistrationOptions(ClientCapabilities clientCapabilities) {
        return new DidChangeWorkspaceFolderRegistrationOptions() {
            // ChangeNotifications = true,
            Supported = false,
        };
    }

    public override Task<Unit> Handle(DidChangeWorkspaceFoldersParams request, CancellationToken cancellationToken) {
        // var added = request.Event.Added.Select(folder => folder.Uri.GetFileSystemPath());
        // if (!added.Any())
        //     return Unit.Value;

        // await workspaceService.WaitHandle;
        // workspaceService.AddWorkspaceFolders(added);
        // workspaceService.StartSolutionLoading();
        // // Automatically skip loaded projects

        return Unit.Task;
    }
}