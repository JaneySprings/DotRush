using Microsoft.CodeAnalysis.CodeActions;
using DotRush.Server.Services;
using ProtocolModels = OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol;
using System.Collections.Immutable;
using System.Reflection;

namespace DotRush.Server.Extensions;

public static class CodeActionConverter {
    private static PropertyInfo? nestedCodeActionsProperty;
    private static PropertyInfo? priorityProperty;
    private static FieldInfo? inNewFileField;

    public static IEnumerable<CodeAction> ToSingleCodeActions(this CodeAction codeAction) {
        var result = new List<CodeAction>();
        
        if (nestedCodeActionsProperty == null)
            nestedCodeActionsProperty = typeof(CodeAction).GetProperty("NestedCodeActions", BindingFlags.Instance | BindingFlags.NonPublic);

        var nesteadCodeActionsObject = nestedCodeActionsProperty?.GetValue(codeAction);
        if (nesteadCodeActionsObject != null && nesteadCodeActionsObject is ImmutableArray<CodeAction> nesteadCodeActions && nesteadCodeActions.Length > 0) {
            result.AddRange(nesteadCodeActions.SelectMany(x => x.ToSingleCodeActions()));
            return result;
        }

        result.Add(codeAction);
        return result;
    }

    public static ProtocolModels.CodeAction ToCodeAction(this CodeAction codeAction) {
        if (priorityProperty == null)
            priorityProperty = typeof(CodeAction).GetProperty("Priority", BindingFlags.Instance | BindingFlags.NonPublic);

        var priority = priorityProperty?.GetValue(codeAction);
        var IsPreferred = priority != null && (int)priority == 3;

        return new ProtocolModels.CodeAction() {
            Kind = ProtocolModels.CodeActionKind.QuickFix,
            Data = codeAction.EquivalenceKey,
            Title = codeAction.Title,
            IsPreferred = IsPreferred,
        };
    }

    public static async Task<ProtocolModels.CodeAction?> ToCodeActionAsync(this CodeAction codeAction, WorkspaceService solutionService, CancellationToken cancellationToken) {
        if (solutionService.Solution == null)
            return null;

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

    public static bool IsBlacklisted(this CodeAction codeAction) {
        var actionType = codeAction.GetType();
        var actionName = actionType.Name;
        if (actionName == "GenerateTypeCodeActionWithOption" || actionName == "ChangeSignatureCodeAction" || actionName == "PullMemberUpWithDialogCodeAction")
            return true;

        if (actionName != "GenerateTypeCodeAction")
            return false;

        if (inNewFileField == null)
            inNewFileField = actionType.GetField("_inNewFile", BindingFlags.Instance | BindingFlags.NonPublic);

        var isNewFile = inNewFileField?.GetValue(codeAction);
        if (isNewFile != null && (bool)isNewFile)
            return true;

        return false;
    }


    public static bool ContainsWithMapping(this ImmutableArray<string> array, string item) {
        if (item == "CS8019")
            return array.Contains("RemoveUnnecessaryImportsFixable");

        return array.Contains(item);
    }
}