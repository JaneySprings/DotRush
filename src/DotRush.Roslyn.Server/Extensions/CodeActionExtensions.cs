using DotRush.Common.Extensions;
using DotRush.Common.Logging;
using DotRush.Roslyn.CodeAnalysis.Extensions;
using DotRush.Roslyn.Workspaces;
using DotRush.Roslyn.Workspaces.Extensions;
using EmmyLua.LanguageServer.Framework.Protocol.Model;
using EmmyLua.LanguageServer.Framework.Protocol.Model.TextEdit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using FileSystemExtensions = DotRush.Common.Extensions.FileSystemExtensions;
using ProtocolModels = EmmyLua.LanguageServer.Framework.Protocol.Message.CodeAction;

namespace DotRush.Roslyn.Server.Extensions;

public static class CodeActionExtensions {
    public static ProtocolModels.CodeAction ToCodeAction(this CodeAction codeAction, ProtocolModels.CodeActionKind kind) {
        return new ProtocolModels.CodeAction() {
            IsPreferred = codeAction.Priority == CodeActionPriority.High,
            Kind = kind,
            Title = codeAction.Title,
            Data = codeAction.GetUniqueId(),
        };
    }
    private static ProtocolModels.CodeAction ToCodeAction(this CodeAction codeAction, ProtocolModels.CodeActionKind kind, string title) {
        return new ProtocolModels.CodeAction() {
            IsPreferred = codeAction.Priority == CodeActionPriority.High,
            Kind = kind,
            Title = title,
            Data = codeAction.GetUniqueId(),
        };
    }

    public static IEnumerable<Tuple<CodeAction, ProtocolModels.CodeAction>> ToFlattenCodeActions(this CodeAction codeAction, ProtocolModels.CodeActionKind kind) {
        if (codeAction.NestedActions.IsEmpty)
            return new[] { new Tuple<CodeAction, ProtocolModels.CodeAction>(codeAction, codeAction.ToCodeAction(kind)) };

        return codeAction.NestedActions.SelectMany(it => it.ToFlattenCodeActionsCore(kind, codeAction.Title));
    }
    private static IEnumerable<Tuple<CodeAction, ProtocolModels.CodeAction>> ToFlattenCodeActionsCore(this CodeAction codeAction, ProtocolModels.CodeActionKind kind, string parentTitle) {
        if (codeAction.NestedActions.IsEmpty)
            return new[] { new Tuple<CodeAction, ProtocolModels.CodeAction>(codeAction, codeAction.ToCodeAction(kind, codeAction.GetSubject(parentTitle))) };

        return codeAction.NestedActions.SelectMany(it => it.ToFlattenCodeActionsCore(kind, codeAction.GetSubject(parentTitle)));
    }
    private static string GetSubject(this CodeAction codeAction, string parentTitle) {
        if (string.IsNullOrEmpty(parentTitle))
            return codeAction.Title;
        if (codeAction.Title.StartsWithUpper())
            return codeAction.Title;

        return $"{parentTitle} {codeAction.Title}";
    }
    // Requires a specific service to get operations (Not available in the current workspace)
    public static bool IsBlacklisted(this CodeAction codeAction) {
        var codeActionName = codeAction.GetType().Name;
        return codeActionName == "GenerateTypeCodeActionWithOption"
            || codeActionName == "ChangeSignatureCodeAction"
            || codeActionName == "ExtractInterfaceCodeAction"
            || codeActionName == "GenerateOverridesWithDialogCodeAction"
            || codeActionName == "GenerateConstructorWithDialogCodeAction"
            || codeActionName == "PullMemberUpWithDialogCodeAction";
    }
}