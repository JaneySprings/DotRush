using DotRush.Server.Extensions;
using LanguageServer.Client;
using Microsoft.CodeAnalysis;

namespace DotRush.Server.Services;

public class CompilationService {
    public static CompilationService Instance { get; private set; } = null!;
    private bool isActive = false;

    private CompilationService() {}

    public static void Initialize() {
        var service = new CompilationService();
        Instance = service;
    }

    public async void Compile(string path, Proxy proxy) {
        if (isActive) 
            return;

        isActive = true;
        //await Task.Delay(CompilationDelay);
        var document = DocumentService.Instance.GetDocumentByPath(path);
        if (document == null) {
            isActive = false;
            return;
        }
        var compilation = await document.Project.GetCompilationAsync();
        if (compilation == null) {
            isActive = false;
            return;
        }

        var diagnostics = compilation.GetDiagnostics().ToServerDiagnostics();
        foreach (var doc in document.Project.Documents) {
            var documentDiagnostics = new List<LanguageServer.Parameters.TextDocument.Diagnostic>();
            documentDiagnostics.AddRange(diagnostics.Where(diagnostic => diagnostic.source == doc.FilePath));
            proxy.TextDocument.PublishDiagnostics(new LanguageServer.Parameters.TextDocument.PublishDiagnosticsParams() {
                uri = new Uri(doc.FilePath!),
                diagnostics = documentDiagnostics.ToArray(),
            });
        }
        isActive = false;
    }
}