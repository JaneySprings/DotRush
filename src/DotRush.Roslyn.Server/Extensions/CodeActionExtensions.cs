using DotRush.Common.Extensions;
using DotRush.Roslyn.CodeAnalysis.Extensions;
using Microsoft.CodeAnalysis.CodeActions;
using ProtocolModels = EmmyLua.LanguageServer.Framework.Protocol.Message.CodeAction;

namespace DotRush.Roslyn.Server.Extensions;

public static class CodeActionExtensions {
    public static ProtocolModels.CodeAction ToCodeAction(this CodeAction codeAction, ProtocolModels.CodeActionKind kind, string? title = null) {
        return new ProtocolModels.CodeAction() {
            IsPreferred = codeAction.Priority == CodeActionPriority.High,
            Kind = kind,
            Title = title ?? codeAction.Title,
            Data = codeAction.GetUniqueId(),
        };
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

    public static void ToFlattenCodeActions(this CodeAction codeAction, Action<CodeAction, string> handler) {
        ToFlattenCore(codeAction, handler, null);

        static void ToFlattenCore(CodeAction codeAction, Action<CodeAction, string> handler, string? parentTitle) {
            if (codeAction.IsBlacklisted())
                return;
            if (codeAction.NestedActions.IsEmpty) {
                handler.Invoke(codeAction, codeAction.Title);
                return;
            }
            foreach (var nestedAction in codeAction.NestedActions)
                ToFlattenCore(nestedAction, handler, GetSubject(parentTitle, codeAction.Title));
        }
        static string GetSubject(string? parentTitle, string currentTitle) {
            if (string.IsNullOrEmpty(parentTitle))
                return currentTitle;
            return currentTitle.StartsWithUpper() ? currentTitle : $"{parentTitle} {currentTitle}";
        }
    }
}