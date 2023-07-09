using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.Text;

namespace DotRush.Server.Extensions;

public class FileCodeAction {
    public CodeAction CodeAction { get; set; }
    public TextSpan TextSpan { get; set; }
    public string FilePath { get; set; }
    public int Id { get; set; }

    public FileCodeAction(CodeAction codeAction, TextSpan span, string filePath) {
        Id = $"{filePath}:{span.Start}:{span.End}:{codeAction.EquivalenceKey}".GetHashCode();
        FilePath = filePath;
        CodeAction = codeAction;
        TextSpan = span;
    }

    public override int GetHashCode() {
        return Id;
    }

    public override bool Equals(object? obj) {
        if (obj is not FileCodeAction other)
            return false;
        
        return Id == other.Id;
    }
}