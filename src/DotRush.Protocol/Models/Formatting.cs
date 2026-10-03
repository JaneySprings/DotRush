namespace DotRush.Protocol.Models;

public class DocumentFormattingParams {
    public TextDocumentIdentifier TextDocument { get; set; } = null!;
    public FormattingOptions Options { get; set; } = null!;
}

public class DocumentRangeFormattingParams {
    public TextDocumentIdentifier TextDocument { get; set; } = null!;
    public DocumentRange Range { get; set; }
    public FormattingOptions Options { get; set; } = null!;
}

public class DocumentRangesFormattingParams {
    public TextDocumentIdentifier TextDocument { get; set; } = null!;
    public List<DocumentRange> Ranges { get; set; } = null!;
    public FormattingOptions Options { get; set; } = null!;
}

public class FormattingOptions {
    public int TabSize { get; set; }
    public bool InsertSpaces { get; set; }
}
