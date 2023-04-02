using dotRush.Server.Extensions;
using LanguageServer.Client;
using Microsoft.CodeAnalysis;

namespace dotRush.Server.Services {

    public class CompilationService {
        private const int CompilationDelay = 500;
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
            await Task.Delay(CompilationDelay);
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

            var diagnostics = compilation.GetDiagnostics().ToServerDiagnostics();
            foreach (var doc in document.Project.Documents) {
                var diagnosticForDoc = diagnostics.Where(diagnostic => diagnostic.source == doc.FilePath);
                proxy.TextDocument.PublishDiagnostics(new LanguageServer.Parameters.TextDocument.PublishDiagnosticsParams() {
                    uri = new Uri(doc.FilePath!),
                    diagnostics = diagnosticForDoc.ToArray(),
                });
            }
            isActive = false;
        }
    }
}