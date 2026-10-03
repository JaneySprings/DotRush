namespace DotRush.Protocol.Models;

public class DocumentSymbolParams {
    public TextDocumentIdentifier TextDocument { get; set; } = null!;
}

public class DocumentSymbol {
    public string Name { get; set; } = string.Empty;
    public string? Detail { get; set; }
    public SymbolKind Kind { get; set; }
    public List<SymbolTag>? Tags { get; set; }
    public DocumentRange Range { get; set; }
    public DocumentRange SelectionRange { get; set; }
    public List<DocumentSymbol>? Children { get; set; }
}

public enum SymbolTag {
    Deprecated = 1
}

public enum SymbolKind {
    File = 1,
    Module = 2,
    Namespace = 3,
    Package = 4,
    Class = 5,
    Method = 6,
    Property = 7,
    Field = 8,
    Constructor = 9,
    Enum = 10,
    Interface = 11,
    Function = 12,
    Variable = 13,
    Constant = 14,
    String = 15,
    Number = 16,
    Boolean = 17,
    Array = 18,
    Object = 19,
    Key = 20,
    Null = 21,
    EnumMember = 22,
    Struct = 23,
    Event = 24,
    Operator = 25,
    TypeParameter = 26
}
