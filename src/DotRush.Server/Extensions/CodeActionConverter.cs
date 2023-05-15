using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using DotRush.Server.Services;
using ProtocolModels = OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol;

namespace DotRush.Server.Extensions;

public static class CodeActionConverter {
    public static ProtocolModels.CodeAction ToCodeAction(this CodeAction codeAction) {
        var textDocumentEdits = new List<ProtocolModels.WorkspaceEditDocumentChange>();
        return new ProtocolModels.CodeAction() {
            Kind = ProtocolModels.CodeActionKind.QuickFix,
            Data = codeAction.GetHashCode(),
            Title = codeAction.Title,
        };
    }

    public static async Task<ProtocolModels.CodeAction?> ToCodeAction(this CodeAction codeAction, Document document, SolutionService solutionService, CancellationToken cancellationToken) {
        var worspaceEdit = new ProtocolModels.WorkspaceEdit();
        var textDocumentEdits = new List<ProtocolModels.WorkspaceEditDocumentChange>();
        var sourceText = await document.GetTextAsync(cancellationToken);
        
        try {
            var operations = await codeAction.GetOperationsAsync(cancellationToken);
            foreach (var operation in operations) {
                if (operation is ApplyChangesOperation applyChangesOperation) {
                    var solutionChanges = applyChangesOperation.ChangedSolution.GetChanges(solutionService.Solution!);
                    foreach (var projectChanges in solutionChanges.GetProjectChanges()) {
                        foreach (var documentChanges in projectChanges.GetChangedDocuments()) {
                            var newDocument = projectChanges.NewProject.GetDocument(documentChanges)!;
                            var text = await newDocument.GetTextAsync(cancellationToken);
                            var textEdits = new List<ProtocolModels.TextEdit>();
                            var textChanges = await newDocument.GetTextChangesAsync(document, cancellationToken);
                            foreach (var textChange in textChanges) {
                                textEdits.Add(new ProtocolModels.TextEdit() {
                                    NewText = textChange.NewText ?? string.Empty,
                                    Range = textChange.Span.ToRange(sourceText),
                                });
                            }
                            textDocumentEdits.Add(new ProtocolModels.TextDocumentEdit() { 
                                Edits = textEdits,
                                TextDocument = new ProtocolModels.OptionalVersionedTextDocumentIdentifier() { 
                                    Uri = DocumentUri.From(newDocument.FilePath!)
                                }
                            });
                        }
                    }
                }
            }

            return new ProtocolModels.CodeAction() {
                Kind = ProtocolModels.CodeActionKind.QuickFix,
                Title = codeAction.Title,
                Edit = new ProtocolModels.WorkspaceEdit() {
                    DocumentChanges = textDocumentEdits,
                },
            };
        } catch (Exception e) {
            LoggingService.Instance.LogError(e.Message, e);
            return null;
        }
    }
}