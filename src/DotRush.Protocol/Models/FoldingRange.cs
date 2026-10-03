namespace DotRush.Protocol.Models;

public class FoldingRangeParams {
    public TextDocumentIdentifier TextDocument { get; set; } = null!;
}

public class FoldingRange {
    public uint StartLine { get; set; }
    public uint? StartCharacter { get; set; }
    public uint EndLine { get; set; }
    public uint? EndCharacter { get; set; }
    public string? CollapsedText { get; set; }
}
