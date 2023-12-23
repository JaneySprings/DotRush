using Microsoft.CodeAnalysis.MSBuild;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server.WorkDone;

namespace DotRush.Server.Services;

public class ProgressObserver: IProgress<ProjectLoadProgress> {
    private IWorkDoneObserver? progressObserver;
    private string formatterMessage;

    public ProgressObserver(string formatterMessage, IWorkDoneObserver? progressObserver) {
        this.progressObserver = progressObserver;
        this.formatterMessage = formatterMessage;
    }

    void IProgress<ProjectLoadProgress>.Report(ProjectLoadProgress value) {
        var projectName = Path.GetFileNameWithoutExtension(value.FilePath);
        progressObserver?.OnNext(new WorkDoneProgressReport { Message = string.Format(formatterMessage, projectName)});
    }
}