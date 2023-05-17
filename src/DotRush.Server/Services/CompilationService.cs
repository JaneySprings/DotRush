using DotRush.Server.Extensions;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using CodeAnalysis = Microsoft.CodeAnalysis;
using OmniSharp.Extensions.LanguageServer.Protocol;

namespace DotRush.Server.Services;

public class CompilationService {
    private SolutionService solutionService;

    public CompilationService(SolutionService solutionService) {
        this.solutionService = solutionService;
    }

    public async void DiagnoseAll(ITextDocumentLanguageServer proxy, CancellationToken cancellationToken) {
        var projects = this.solutionService.Solution?.Projects;
        if (projects == null)
            return;

        var result = new Dictionary<string, List<CodeAnalysis.Diagnostic>>();
        foreach (var project in projects) {
           foreach (var document in project.Documents) {
                var diagnostics = await Diagnose(document, cancellationToken);
                if (result.ContainsKey(document.FilePath!))
                    result[document.FilePath!].AddRange(diagnostics!);
                else
                    result.Add(document.FilePath!, diagnostics!.ToList());
            }
        }

        foreach (var diagnostic in result) {
            proxy.PublishDiagnostics(new PublishDiagnosticsParams() {
                Uri = DocumentUri.From(diagnostic.Key),
                Diagnostics = new Container<Diagnostic>(diagnostic.Value.ToServerDiagnostics()),
            });
        }
    }

    public async Task DiagnoseDocument(string documentPath, ITextDocumentLanguageServer proxy, CancellationToken cancellationToken) {
        var documentIds = this.solutionService.Solution?.GetDocumentIdsWithFilePath(documentPath);
        if (documentIds == null)
            return;

        var result = new Dictionary<string, List<CodeAnalysis.Diagnostic>>();
        foreach (var documentId in documentIds) {
            var document = this.solutionService.Solution?.GetDocument(documentId);
            if (document == null)
                continue;

            var diagnostics = await Diagnose(document, cancellationToken);
            if (result.ContainsKey(document.FilePath!))
                result[document.FilePath!].AddRange(diagnostics!);
            else
                result.Add(document.FilePath!, diagnostics!.ToList());
        }

        foreach (var diagnostic in result) {
            proxy.PublishDiagnostics(new PublishDiagnosticsParams() {
                Uri = DocumentUri.From(diagnostic.Key),
                Diagnostics = new Container<Diagnostic>(diagnostic.Value.ToServerDiagnostics()),
            });
        }
    }

    public async Task<IEnumerable<CodeAnalysis.Diagnostic>?> Diagnose(CodeAnalysis.Document document, CancellationToken cancellationToken) {
        var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
        return semanticModel?
            .GetDiagnostics(cancellationToken: cancellationToken)
            .Where(d => File.Exists(d.Location.SourceTree?.FilePath));
    }
}