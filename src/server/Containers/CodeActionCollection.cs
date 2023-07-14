using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.Text;

namespace DotRush.Server.Containers;

public class CodeActionCollection {
    private readonly HashSet<FileCodeAction> codeActionCollection;

    public CodeActionCollection() {
        codeActionCollection = new HashSet<FileCodeAction>();
    }

    public int AddCodeAction(CodeAction codeAction, TextSpan span, string filePath) {
        var item = new FileCodeAction(codeAction, span, filePath);
        if (codeActionCollection.Add(item))
            return item.Id;
        return -1;
    }

    public CodeAction? GetCodeAction(int id) {
        return codeActionCollection.FirstOrDefault(x => x.Id == id)?.CodeAction;
    }

    public void ClearWithFilePath(string filePath) {
        codeActionCollection.RemoveWhere(x => x.FilePath == filePath);
    }

    public void ClearWithFilePathWithSpan(string filePath, TextSpan span) {
        codeActionCollection.RemoveWhere(x => x.FilePath == filePath && x.TextSpan == span);
    }
}