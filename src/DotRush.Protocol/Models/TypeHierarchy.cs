namespace DotRush.Protocol.Models;

public class TypeHierarchyPrepareParams : TextDocumentPositionParams {
}

public class TypeHierarchySupertypesParams {
    public TypeHierarchyItem Item { get; set; } = null!;
}

public class TypeHierarchySubtypesParams {
    public TypeHierarchyItem Item { get; set; } = null!;
}

public class TypeHierarchyItem {
    public string Name { get; set; } = string.Empty;
    public SymbolKind Kind { get; set; }
    public List<SymbolTag>? Tags { get; set; }
    public string? Detail { get; set; }
    public DocumentUri Uri { get; set; }
    public DocumentRange Range { get; set; }
    public DocumentRange SelectionRange { get; set; }
    public LSPAny? Data { get; set; }
}
