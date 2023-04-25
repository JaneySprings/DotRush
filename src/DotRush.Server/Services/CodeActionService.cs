using System.Reflection;
using DotRush.Server.Extensions;
using Microsoft.CodeAnalysis.CodeFixes;
using LanguageServer.Parameters.TextDocument;
using CodeAnalysis = Microsoft.CodeAnalysis;

namespace DotRush.Server.Services;

public class CodeActionService {
    public static CodeActionService Instance { get; private set; } = null!;
    private HashSet<CodeFixProvider> CodeFixProviders { get; set; }

    private CodeActionService() {
        CodeFixProviders = new HashSet<CodeFixProvider>();
    }
    public static void Initialize() {
        Instance = new CodeActionService();
        var providersLocations = new List<string>() {
            "Microsoft.CodeAnalysis.CSharp.Features",
            "Microsoft.CodeAnalysis.CSharp.Workspaces",
            "Microsoft.CodeAnalysis.Workspaces",
            "Microsoft.CodeAnalysis.Features"
        };

        foreach (var location in providersLocations) {
            var assembly = Assembly.Load(location);
            var providers = assembly.DefinedTypes
                .Where(x => !x.IsAbstract && x.IsSubclassOf(typeof(CodeFixProvider)))
                .Select(x => {
                    try {
                        var attribute = x.GetCustomAttribute<ExportCodeFixProviderAttribute>();
                        if (attribute == null) {
                            LoggingService.Instance.LogMessage($"Skipping code fix provider '{x.AsType()}' because it is missing the ExportCodeFixProviderAttribute.");
                            return null;
                        }

                        if (attribute.Languages == null) {
                            LoggingService.Instance.LogMessage($"Skipping code fix provider '{x.AsType()}' because its language '{attribute.Languages?.FirstOrDefault()}' doesn't specified.");
                            return null;
                        }

                        return Activator.CreateInstance(x.AsType()) as CodeFixProvider;
                    } catch (Exception ex) {
                        LoggingService.Instance.LogError($"Creating instance of code fix provider '{x.AsType()}' failed, error: {ex}");
                        return null;
                    }
                }).Where(x => x != null);
            
            foreach (var provider in providers)
                Instance.CodeFixProviders.Add(provider!);
        }
    }

    public IEnumerable<CodeAction> GetCodeFixes(string documentPath, Diagnostic[] diagnostics) {
        var codeActions = new List<CodeAction?>();
        var document = DocumentService.GetDocumentByPath(documentPath);
        if (diagnostics == null || diagnostics.Length == 0 || document == null)
            return codeActions!;

        foreach (var diagnostic in diagnostics) {
            var fileDiagnostic = GetDiagnosticByRange(diagnostic.range, document);
            var codeFixProviders = GetProvidersForDiagnosticId(fileDiagnostic?.Id);
            if (fileDiagnostic == null || codeFixProviders == null || !codeFixProviders.Any())
                continue;

            foreach (var codeFixProvider in codeFixProviders) {
                var context = new CodeFixContext(document, fileDiagnostic, (a, _) => codeActions.Add(a.ToCodeAction(document, diagnostics)), CancellationToken.None);
                codeFixProvider.RegisterCodeFixesAsync(context).Wait();
            }
        }

        return codeActions.Where(x => x != null).OrderByDescending(x => x!.title)!;
    }

    private IEnumerable<CodeFixProvider>? GetProvidersForDiagnosticId(string? diagnosticId) {
        if (diagnosticId == null)
            return null;

        return CodeFixProviders?.Where(x => x.FixableDiagnosticIds.Contains(diagnosticId));
    }

    private CodeAnalysis.Diagnostic? GetDiagnosticByRange(LanguageServer.Parameters.Range range, CodeAnalysis.Document document) {
        var diagnostics = CompilationService.Instance.Diagnostics.TryGetValue(document.FilePath!, out var diags) ? diags : null;
        if (diagnostics == null)
            return null;

        return diagnostics.FirstOrDefault(x => x.Location.SourceSpan.ToRange(document).IsEqualTo(range));
    }
}