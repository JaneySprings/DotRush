using dotRush.Server.Extensions;
using LanguageServer.Client;
using Microsoft.CodeAnalysis;

namespace dotRush.Server.Services {

    public class CompilationService {
        public static CompilationService? Instance { get; private set; }
        private bool isActive = false;

        private CompilationService() {}

        public static async Task Initialize() {
            var service = new CompilationService();
            Instance = service;
        }

        public async void Compile(string path, Proxy proxy) {
            if (isActive) 
                return;

            isActive = true;
            var documentId = SolutionService.Instance!.CurrentSolution?.GetDocumentIdsWithFilePath(path).FirstOrDefault();
            var document = SolutionService.Instance.CurrentSolution?.GetDocument(documentId);
            if (documentId == null || document == null) {
                isActive = false;
                return;
            }
            var compilation = await document.Project.GetCompilationAsync();
            if (compilation == null) {
                isActive = false;
                return;
            }

            ClearDiagnostics(document.Project, proxy);

            var diagnostics = compilation.GetDiagnostics().ToServerDiagnostics();
            var diagnosticsByFile = diagnostics.GroupBy(diagnostic => diagnostic.source);
            foreach (var diagnosticGroup in diagnosticsByFile) {
                proxy.TextDocument.PublishDiagnostics(new LanguageServer.Parameters.TextDocument.PublishDiagnosticsParams() {
                    uri = new Uri(diagnosticGroup.Key),
                    diagnostics = diagnosticGroup.ToArray(),
                });
            }
            isActive = false;
        }

        public void ClearDiagnostics(Project project, Proxy proxy) {
            foreach (var document in project.Documents) {
                proxy.TextDocument.PublishDiagnostics(new LanguageServer.Parameters.TextDocument.PublishDiagnosticsParams() {
                    diagnostics = Array.Empty<LanguageServer.Parameters.TextDocument.Diagnostic>(),
                    uri = new Uri(document.FilePath!)
                });
            }
        }
    }
}