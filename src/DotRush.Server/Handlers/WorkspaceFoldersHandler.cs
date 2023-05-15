using DotRush.Server.Services;
using MediatR;
using Microsoft.CodeAnalysis;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Workspace;

namespace DotRush.Server.Handlers;

public class WorkspaceFoldersHandler : DidChangeWorkspaceFoldersHandlerBase {
    private SolutionService solutionService;

    public WorkspaceFoldersHandler(SolutionService solutionService) {
        this.solutionService = solutionService;
    }

    protected override DidChangeWorkspaceFolderRegistrationOptions CreateRegistrationOptions(ClientCapabilities clientCapabilities){
        return new DidChangeWorkspaceFolderRegistrationOptions() {
            ChangeNotifications = true,
            Supported = true,
        };
    }

    public override async Task<Unit> Handle(DidChangeWorkspaceFoldersParams request, CancellationToken cancellationToken) {
        var added = request.Event.Added.Select(folder => folder.Uri.GetFileSystemPath());
        var removed = request.Event.Removed.Select(folder => folder.Uri.GetFileSystemPath());

        foreach (var remove in removed)
            this.solutionService.RemoveProjects(Directory.GetFiles(remove, "*.csproj", SearchOption.AllDirectories));
        foreach (var add in added)
            this.solutionService.AddProjects(Directory.GetFiles(add, "*.csproj", SearchOption.AllDirectories));

        await this.solutionService.ReloadSolution(cancellationToken);
        return Unit.Value;
    } 
}