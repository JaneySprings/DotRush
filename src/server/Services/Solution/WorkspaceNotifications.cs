using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Extensions.LanguageServer.Protocol.Server.WorkDone;
using OmniSharp.Extensions.LanguageServer.Protocol.Window;

namespace DotRush.Server.Services;

public class WorkspaceNotifications : IProgress<ProjectLoadProgress> {
    private const int MAX_WORKSPACE_ERRORS = 10;

    private IWorkDoneObserver? observer;
    private ILanguageServerFacade? serverFacade;
    private int workspaceErrors = 0;

    public void SetWorkDoneObserver(IWorkDoneObserver? observer) {
        this.observer = observer;
    }
    public void SetLanguageServerFacade(ILanguageServerFacade? serverFacade) {
        this.serverFacade = serverFacade;
    }

    public void NotifyWorkspaceFailed(object? sender, WorkspaceDiagnosticEventArgs e) {
        NotifyWorkspaceFailed(e.Diagnostic.Message);
    }
    public void NotifyWorkspaceFailed(string message) {
        if (workspaceErrors < MAX_WORKSPACE_ERRORS) {
            serverFacade?.Window.ShowWarning(message);
            workspaceErrors++;
        }
    }
    public void ResetWorkspaceErrors() {
        workspaceErrors = 0;
    }

    void IProgress<ProjectLoadProgress>.Report(ProjectLoadProgress value) {
        var projectName = Path.GetFileNameWithoutExtension(value.FilePath);
        observer?.OnNext(new WorkDoneProgressReport { Message = $"Indexing {projectName}"});
    }
}