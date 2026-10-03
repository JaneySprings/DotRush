namespace DotRush.Protocol.Models;

public class ServerCapabilities {
    public TextDocumentSyncOptions? TextDocumentSync { get; set; }
    public CompletionOptions? CompletionProvider { get; set; }
    public bool? HoverProvider { get; set; }
    public SignatureHelpOptions? SignatureHelpProvider { get; set; }
    public bool? DefinitionProvider { get; set; }
    public bool? TypeDefinitionProvider { get; set; }
    public bool? ImplementationProvider { get; set; }
    public bool? ReferencesProvider { get; set; }
    public bool? DocumentSymbolProvider { get; set; }
    public CodeActionOptions? CodeActionProvider { get; set; }
    public bool? DocumentFormattingProvider { get; set; }
    public bool? DocumentRangeFormattingProvider { get; set; }
    public RenameOptions? RenameProvider { get; set; }
    public bool? FoldingRangeProvider { get; set; }
    public SemanticTokensOptions? SemanticTokensProvider { get; set; }
    public bool? TypeHierarchyProvider { get; set; }
    public bool? InlayHintProvider { get; set; }
    public bool? WorkspaceSymbolProvider { get; set; }
}

public class TextDocumentSyncOptions {
    public bool OpenClose { get; set; }
    public TextDocumentSyncKind? Change { get; set; }
}

public class CompletionOptions {
    public List<string>? TriggerCharacters { get; set; }
    public List<string>? AllCommitCharacters { get; set; }
    public bool ResolveProvider { get; set; }
}

public class SignatureHelpOptions {
    public List<string>? TriggerCharacters { get; set; }
    public List<string>? RetriggerCharacters { get; set; }
}

public class RenameOptions {
    public bool PrepareProvider { get; set; }
}

public class CodeActionOptions {
    public List<CodeActionKind>? CodeActionKinds { get; set; }
    public bool ResolveProvider { get; set; }
}

public class SemanticTokensOptions {
    public SemanticTokensLegend Legend { get; set; } = new SemanticTokensLegend();
    public bool? Range { get; set; }
    public bool? Full { get; set; }
}

public class SemanticTokensLegend {
    public List<string> TokenTypes { get; set; } = new List<string>();
    public List<string> TokenModifiers { get; set; } = new List<string>();
}
