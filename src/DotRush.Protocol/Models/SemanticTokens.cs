namespace DotRush.Protocol.Models;

public class SemanticTokensParams {
    public TextDocumentIdentifier TextDocument { get; set; } = null!;
}

public class SemanticTokensRangeParams {
    public TextDocumentIdentifier TextDocument { get; set; } = null!;
    public DocumentRange Range { get; set; }
}

public class SemanticTokens {
    public string? ResultId { get; set; }
    public List<uint> Data { get; set; } = null!;
}
