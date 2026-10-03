using System.Text.Json.Serialization;

namespace DotRush.Protocol.Models;

public record TextDocumentIdentifier(DocumentUri Uri);

public record VersionedTextDocumentIdentifier(DocumentUri Uri, int Version) : TextDocumentIdentifier(Uri);

// The version is required by the specification, so it must be serialized as an explicit 'null' when it is unknown
public record OptionalVersionedTextDocumentIdentifier(DocumentUri Uri, [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] int? Version) : TextDocumentIdentifier(Uri);

public class TextDocumentItem {
    public DocumentUri Uri { get; set; }
    public string LanguageId { get; set; } = string.Empty;
    public int Version { get; set; }
    public string Text { get; set; } = string.Empty;
}

public class TextDocumentPositionParams {
    public TextDocumentIdentifier TextDocument { get; set; } = null!;
    public Position Position { get; set; }
}

public class DidOpenTextDocumentParams {
    public TextDocumentItem TextDocument { get; set; } = null!;
}

public class DidChangeTextDocumentParams {
    public VersionedTextDocumentIdentifier TextDocument { get; set; } = null!;
    public List<TextDocumentContentChangeEvent> ContentChanges { get; set; } = null!;
}

public class DidCloseTextDocumentParams {
    public TextDocumentIdentifier TextDocument { get; set; } = null!;
}

public class TextDocumentContentChangeEvent {
    public DocumentRange? Range { get; set; }
    public string Text { get; set; } = string.Empty;
}

public enum TextDocumentSyncKind {
    None = 0,
    Full = 1,
    Incremental = 2
}
