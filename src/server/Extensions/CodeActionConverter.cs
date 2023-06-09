using Microsoft.CodeAnalysis.CodeActions;
using DotRush.Server.Services;
using ProtocolModels = OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol;
using System.Collections.Immutable;

namespace DotRush.Server.Extensions;

public static class CodeActionConverter {
    public static ProtocolModels.CodeAction ToCodeAction(this CodeAction codeAction, int id) {
        var textDocumentEdits = new List<ProtocolModels.WorkspaceEditDocumentChange>();
        return new ProtocolModels.CodeAction() {
            Kind = ProtocolModels.CodeActionKind.QuickFix,
            Title = codeAction.Title,
            Data = id,
        };
    }

    public static async Task<ProtocolModels.CodeAction?> ToCodeActionAsync(this CodeAction codeAction, SolutionService solutionService, CancellationToken cancellationToken) {
        if (solutionService.Solution == null)
            return null;
        
        var worspaceEdit = new ProtocolModels.WorkspaceEdit();
        var textDocumentEdits = new List<ProtocolModels.WorkspaceEditDocumentChange>();
        var operations = await codeAction.GetOperationsAsync(cancellationToken);
        foreach (var operation in operations) {
            if (operation is ApplyChangesOperation applyChangesOperation) {
                var solutionChanges = applyChangesOperation.ChangedSolution.GetChanges(solutionService.Solution);
                foreach (var projectChanges in solutionChanges.GetProjectChanges()) {
                    foreach (var documentChanges in projectChanges.GetChangedDocuments()) {
                        var newDocument = projectChanges.NewProject.GetDocument(documentChanges);
                        var oldDocument = solutionService.Solution?.GetDocument(newDocument?.Id);
                        if (oldDocument?.FilePath == null || newDocument?.FilePath == null)
                            continue;

                        var sourceText = await oldDocument.GetTextAsync(cancellationToken);
                        var text = await newDocument.GetTextAsync(cancellationToken);
                        var textEdits = new List<ProtocolModels.TextEdit>();
                        var textChanges = await newDocument.GetTextChangesAsync(oldDocument, cancellationToken);
                        foreach (var textChange in textChanges) {
                            textEdits.Add(new ProtocolModels.TextEdit() {
                                NewText = textChange.NewText ?? string.Empty,
                                Range = textChange.Span.ToRange(sourceText),
                            });
                        }
                        textDocumentEdits.Add(new ProtocolModels.TextDocumentEdit() { 
                            Edits = textEdits,
                            TextDocument = new ProtocolModels.OptionalVersionedTextDocumentIdentifier() { 
                                Uri = DocumentUri.From(newDocument.FilePath)
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
    }

    public static bool ContainsWithMapping(this ImmutableArray<string> array, string item) {
        if (item == "CS8019")
            return array.Contains("RemoveUnnecessaryImportsFixable");

        return array.Contains(item);
    }
}