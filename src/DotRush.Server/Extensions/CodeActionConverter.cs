using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;

namespace DotRush.Server.Extensions;

public static class CodeActionConverter {
    public static LanguageServer.Parameters.TextDocument.CodeAction ToCodeAction(this CodeAction codeAction, Document document, LanguageServer.Parameters.TextDocument.Diagnostic[] diagnostics) {
        // var worspaceEdit = new LanguageServer.Parameters.WorkspaceEdit();
        // var changes = new Dictionary<string, LanguageServer.Parameters.TextEdit[]>();

        return new LanguageServer.Parameters.TextDocument.CodeAction() {
            kind = LanguageServer.Parameters.CodeActionKind.QuickFix,
            title = codeAction.Title,
            diagnostics = diagnostics,
        };
    }
}