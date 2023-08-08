using DotRush.Server.Extensions;
using Microsoft.CodeAnalysis.MSBuild;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server.WorkDone;

namespace DotRush.Server.Services;

public class ProgressNotification : IProgress<ProjectLoadProgress> {
    private IWorkDoneObserver? observer;

    public ProgressNotification(IWorkDoneObserver? observer) {
        this.observer = observer;
    }

    void IProgress<ProjectLoadProgress>.Report(ProjectLoadProgress value) {
        var projectName = Path.GetFileNameWithoutExtension(value.FilePath);
        var operation = value.Operation.ToOperationString();
        observer?.OnNext(new WorkDoneProgressReport { Message = $"{operation} {projectName}"});
    }
}