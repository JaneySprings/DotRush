using DotRush.Server.Extensions;
using LanguageServer.Client;
using Microsoft.CodeAnalysis;

namespace DotRush.Server.Services;

public class CompilationService {
    public static CompilationService Instance { get; private set; } = null!;
    public Dictionary<string, IEnumerable<Diagnostic>> Diagnostics { get; private set; }
    public HashSet<string> DiagnosedDocuments { get; private set; }
    private bool isActive = false;

    private CompilationService() { 
        Diagnostics = new Dictionary<string, IEnumerable<Diagnostic>>();
        DiagnosedDocuments = new HashSet<string>();
    }

    public static void Initialize() {
        Instance = new CompilationService();
    }

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

    public async void Diagnose(Proxy proxy) {
        if (isActive) 
           return;

        isActive = true;
        foreach (var targetDocument in DiagnosedDocuments) {
            var document = DocumentService.GetDocumentByPath(targetDocument);
            if (document == null)
                continue;

            var semanticModel = await document.GetSemanticModelAsync();
            var diagnostics = semanticModel?.GetDiagnostics();
            var serverDiagnostics = diagnostics?.ToServerDiagnostics();
            if (semanticModel == null || diagnostics == null || serverDiagnostics == null)
                continue;

            if (Diagnostics.ContainsKey(targetDocument))
                Diagnostics[targetDocument] = diagnostics;
            else
                Diagnostics.Add(targetDocument, diagnostics);

            proxy.TextDocument.PublishDiagnostics(new LanguageServer.Parameters.TextDocument.PublishDiagnosticsParams() {
                uri = document.FilePath?.ToUri(),
                diagnostics = serverDiagnostics.ToArray(),
            });
        }
        isActive = false;
    }
}