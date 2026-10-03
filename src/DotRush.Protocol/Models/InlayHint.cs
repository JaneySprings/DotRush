namespace DotRush.Protocol.Models;

public class InlayHintParams {
    public TextDocumentIdentifier TextDocument { get; set; } = null!;
    public DocumentRange Range { get; set; }
}

public class InlayHint {
    public Position Position { get; set; }
    public string Label { get; set; } = string.Empty;
    public InlayHintKind? Kind { get; set; }
    public List<TextEdit>? TextEdits { get; set; }
    public bool? PaddingLeft { get; set; }
    public bool? PaddingRight { get; set; }
}

public enum InlayHintKind {
    Type = 1,
    Parameter = 2
}
