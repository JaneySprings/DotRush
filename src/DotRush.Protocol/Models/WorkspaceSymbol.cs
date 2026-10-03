namespace DotRush.Protocol.Models;

public class WorkspaceSymbolParams {
    public string Query { get; set; } = string.Empty;
}

public class WorkspaceSymbol {
    public string Name { get; set; } = string.Empty;
    public SymbolKind Kind { get; set; }
    public List<SymbolTag>? Tags { get; set; }
    public string? ContainerName { get; set; }
    public Location? Location { get; set; }
}
