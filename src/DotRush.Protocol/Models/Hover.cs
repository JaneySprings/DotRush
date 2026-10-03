namespace DotRush.Protocol.Models;

public class HoverParams : TextDocumentPositionParams {
}

public class Hover {
    public MarkupContent Contents { get; set; } = null!;
    public DocumentRange? Range { get; set; }
}
