using Microsoft.CodeAnalysis.CodeActions;

namespace DotRush.Server.Extensions;

public class FileCodeActions {
    public Dictionary<int, Tuple<string, CodeAction>> CodeActions { get; private set; }

    public FileCodeActions() {
        CodeActions = new Dictionary<int, Tuple<string, CodeAction>>();
    }

    public void SetCodeAction(CodeAction codeAction, int id, string filePath) {
        if (CodeActions.ContainsKey(id))
            CodeActions[id] = new Tuple<string, CodeAction>(filePath, codeAction);
        else
            CodeActions.Add(id, new Tuple<string, CodeAction>(filePath, codeAction));
    }

    public CodeAction? GetCodeAction(int id) {
        CodeAction? result = null;
        if (CodeActions.TryGetValue(id, out var codeAction))
            result = codeAction.Item2;

        CodeActions.Remove(id);
        return result;
    }

    public void ClearWithFilePath(string filePath) {
        var keys = CodeActions.Where(x => x.Value.Item1 == filePath).Select(x => x.Key).ToList();
        foreach (var key in keys)
            CodeActions.Remove(key);
    }
}