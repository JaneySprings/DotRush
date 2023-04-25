using DotRush.Server.Extensions;
using LanguageServer.Client;

namespace DotRush.Server.Services;

public class CompilationService {
    public static async void Compile(string projectPath, Proxy proxy) {
        var project = SolutionService.Instance.GetProjectByPath(projectPath);
        if (project == null) 
            return;

        var compilation = await project.GetCompilationAsync();
        if (compilation == null) 
            return;

        var diagnostics = compilation.GetDiagnostics().ToServerDiagnostics();
        var diagnosticsGroups = diagnostics.GroupBy(diagnostic => diagnostic.source);
        foreach (var diagnosticsGroup in diagnosticsGroups) {
            proxy.TextDocument.PublishDiagnostics(new LanguageServer.Parameters.TextDocument.PublishDiagnosticsParams() {
                uri = diagnosticsGroup.Key.ToUri(),
                diagnostics = diagnosticsGroup.ToArray(),
            });
        }
    }
}