using DotRush.Server.Extensions;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using CodeAnalysis = Microsoft.CodeAnalysis;
using OmniSharp.Extensions.LanguageServer.Protocol;

namespace DotRush.Server.Services;

public class CompilationService {
    public Dictionary<string, IEnumerable<CodeAnalysis.Diagnostic>> Diagnostics { get; private set; }
    public HashSet<string> DiagnosedDocuments { get; private set; }
    private SolutionService solutionService;
    private bool isActive = false;

    public CompilationService(SolutionService solutionService) { 
        Diagnostics = new Dictionary<string, IEnumerable<CodeAnalysis.Diagnostic>>();
        DiagnosedDocuments = new HashSet<string>();
        this.solutionService = solutionService;
    }


    public async void Compile(string projectPath, ITextDocumentLanguageServer proxy) {
        var project = this.solutionService.GetProjectByPath(projectPath);
        if (project == null) 
            return;

        var compilation = await project.GetCompilationAsync();
        if (compilation == null) 
            return;

        var diagnostics = compilation.GetDiagnostics().ToServerDiagnostics();
        var diagnosticsGroups = diagnostics.GroupBy(diagnostic => diagnostic.Source);
        foreach (var diagnosticsGroup in diagnosticsGroups) {
            proxy.PublishDiagnostics(new PublishDiagnosticsParams() {
                Uri = DocumentUri.From(diagnosticsGroup.Key!),
                Diagnostics = new Container<Diagnostic>(diagnosticsGroup),
            });
        }
    }

    public async Task Diagnose(ITextDocumentLanguageServer proxy, CancellationToken cancellationToken) {
        if (isActive) 
           return;

        isActive = true;
        foreach (var targetDocument in DiagnosedDocuments) {
            var document = this.solutionService.GetDocumentByPath(targetDocument);
            if (document == null)
                continue;

            var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
            var diagnostics = semanticModel?.GetDiagnostics(cancellationToken: cancellationToken);
            var serverDiagnostics = diagnostics?.ToServerDiagnostics();
            if (semanticModel == null || diagnostics == null || serverDiagnostics == null)
                continue;

            if (Diagnostics.ContainsKey(targetDocument))
                Diagnostics[targetDocument] = diagnostics;
            else
                Diagnostics.Add(targetDocument, diagnostics);

            proxy.PublishDiagnostics(new PublishDiagnosticsParams() {
                Uri = DocumentUri.From(document.FilePath!),
                Diagnostics = new Container<Diagnostic>(serverDiagnostics),
            });
        }
        isActive = false;
    }
}