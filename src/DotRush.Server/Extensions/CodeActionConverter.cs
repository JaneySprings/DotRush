using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using DotRush.Server.Services;

namespace DotRush.Server.Extensions;

public static class CodeActionConverter {
    public static LanguageServer.Parameters.TextDocument.CodeAction? ToCodeAction(this CodeAction codeAction, Document document, LanguageServer.Parameters.TextDocument.Diagnostic[] diagnostics) {
        var worspaceEdit = new LanguageServer.Parameters.WorkspaceEdit();
        var changes = new Dictionary<Uri, LanguageServer.Parameters.TextEdit[]>();
        
        try {
            var operations = codeAction.GetOperationsAsync(CancellationToken.None).Result;
            foreach (var operation in operations) {
                if (operation is ApplyChangesOperation applyChangesOperation) {
                    var solutionChanges = applyChangesOperation.ChangedSolution.GetChanges(SolutionService.Instance.Solution!);
                    foreach (var projectChanges in solutionChanges.GetProjectChanges()) {
                        foreach (var documentChanges in projectChanges.GetChangedDocuments()) {
                            var newDocument = projectChanges.NewProject.GetDocument(documentChanges)!;
                            var text = newDocument.GetTextAsync().Result;
                            var textEdits = new List<LanguageServer.Parameters.TextEdit>();
                            var textChanges = newDocument.GetTextChangesAsync(document).Result;
                            foreach (var textChange in textChanges) {
                                textEdits.Add(new LanguageServer.Parameters.TextEdit() {
                                    newText = textChange.NewText,
                                    range = textChange.Span.ToRange(document),
                                });
                            }
                            changes.Add(document.FilePath!.ToUri(), textEdits.ToArray());
                        }
                    }
                }
            }

            return new LanguageServer.Parameters.TextDocument.CodeAction() {
                kind = LanguageServer.Parameters.CodeActionKind.QuickFix,
                title = codeAction.Title,
                diagnostics = diagnostics,
                edit = new LanguageServer.Parameters.WorkspaceEdit() {
                    changes = changes,
                },
            };
        } catch (Exception e) {
            LoggingService.Instance.LogError(e.Message, e);
            return null;
        }
    }
}