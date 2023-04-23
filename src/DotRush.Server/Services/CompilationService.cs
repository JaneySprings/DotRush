using DotRush.Server.Extensions;
using LanguageServer.Client;

namespace DotRush.Server.Services;

public class CompilationService {
    public static CompilationService Instance { get; private set; } = null!;
    private bool isActive = false;

    private CompilationService() {}

    public static void Initialize() {
        var service = new CompilationService();
        Instance = service;
    }

    public async void Compile(Proxy proxy) {
        if (isActive) 
            return;

        isActive = true;
        foreach(var projectFile in SolutionService.Instance.ProjectFiles) {
            var project = SolutionService.Instance.GetProjectByPath(projectFile);
            if (project == null) 
                continue;

            var compilation = await project.GetCompilationAsync();
            if (compilation == null) {
                isActive = false;
                return;
            }

            var diagnostics = compilation.GetDiagnostics().ToServerDiagnostics();
            var diagnosticsGroups = diagnostics.GroupBy(diagnostic => diagnostic.source);
            foreach (var diagnosticsGroup in diagnosticsGroups) {
                proxy.TextDocument.PublishDiagnostics(new LanguageServer.Parameters.TextDocument.PublishDiagnosticsParams() {
                    uri = diagnosticsGroup.Key.ToUri(),
                    diagnostics = diagnosticsGroup.ToArray(),
                });
            }           
        }

        isActive = false;
    }
}