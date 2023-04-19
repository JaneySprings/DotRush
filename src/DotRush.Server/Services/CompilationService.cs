using DotRush.Server.Extensions;
using LanguageServer.Client;
using Microsoft.CodeAnalysis;

namespace DotRush.Server.Services;

public class CompilationService {
    public static CompilationService Instance { get; private set; } = null!;
    private readonly HashSet<string> diagnosticsLocations = new HashSet<string>();
    private bool isActive = false;

    private CompilationService() {}

    public static void Initialize() {
        var service = new CompilationService();
        Instance = service;
    }

    public async Task Compile(string path, Proxy proxy) {
        if (isActive) 
            return;

        isActive = true;
        //await Task.Delay(CompilationDelay);
        var document = DocumentService.GetDocumentByPath(path);
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
        
        foreach (var diagnostic in diagnostics) 
            diagnosticsLocations.Add(diagnostic.source);
        foreach (var location in diagnosticsLocations) {
            var documentDiagnostics = new List<LanguageServer.Parameters.TextDocument.Diagnostic>();
            documentDiagnostics.AddRange(diagnostics.Where(diagnostic => diagnostic.source == location));
            proxy.TextDocument.PublishDiagnostics(new LanguageServer.Parameters.TextDocument.PublishDiagnosticsParams() {
                uri = location.ToUri(),
                diagnostics = documentDiagnostics.ToArray(),
            });
        }

        isActive = false;
    }
}