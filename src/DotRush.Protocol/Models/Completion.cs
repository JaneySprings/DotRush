using System.Text.Json.Serialization;

namespace DotRush.Protocol.Models;

public class CompletionParams : TextDocumentPositionParams {
    public CompletionContext? Context { get; set; }
}

public class CompletionContext {
    public CompletionTriggerKind TriggerKind { get; set; }
    public string? TriggerCharacter { get; set; }
}

public class CompletionList {
    public bool IsIncomplete { get; set; }
    public CompletionItemDefaults? ItemDefaults { get; set; }
    public List<CompletionItem> Items { get; set; } = null!;
}

public class CompletionItemDefaults {
    public List<string>? CommitCharacters { get; set; }
    public EditRangeWithInsertReplace? EditRange { get; set; }
    public InsertTextFormat? InsertTextFormat { get; set; }
    public InsertTextMode? InsertTextMode { get; set; }
    public LSPAny? Data { get; set; }
}

public class EditRangeWithInsertReplace {
    public DocumentRange Insert { get; set; }
    public DocumentRange Replace { get; set; }
}

public class CompletionItem {
    public string Label { get; set; } = string.Empty;
    public CompletionItemLabelDetails? LabelDetails { get; set; }
    public CompletionItemKind Kind { get; set; }
    public List<CompletionItemTag>? Tags { get; set; }
    public string? Detail { get; set; }
    public MarkupContent? Documentation { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public bool Deprecated { get; set; }
    public bool? Preselect { get; set; }
    public string? SortText { get; set; }
    public string? FilterText { get; set; }
    public string? InsertText { get; set; }
    public InsertTextFormat? InsertTextFormat { get; set; }
    public InsertTextMode? InsertTextMode { get; set; }
    public TextEditOrInsertReplaceEdit? TextEdit { get; set; }
    public string? TextEditText { get; set; }
    public List<TextEdit>? AdditionalTextEdits { get; set; }
    public List<string>? CommitCharacters { get; set; }
    public Command? Command { get; set; }
    public LSPAny? Data { get; set; }
}

public class CompletionItemLabelDetails {
    public string? Detail { get; set; }
    public string? Description { get; set; }
}

public enum CompletionTriggerKind {
    Invoked = 1,
    TriggerCharacter = 2,
    TriggerForIncompleteCompletions = 3
}

public enum CompletionItemTag {
    Deprecated = 1
}

public enum InsertTextFormat {
    PlainText = 1,
    Snippet = 2
}

public enum InsertTextMode {
    AsIs = 1,
    AdjustIndentation = 2
}

public enum CompletionItemKind {
    Text = 1,
    Method = 2,
    Function = 3,
    Constructor = 4,
    Field = 5,
    Variable = 6,
    Class = 7,
    Interface = 8,
    Module = 9,
    Property = 10,
    Unit = 11,
    Value = 12,
    Enum = 13,
    Keyword = 14,
    Snippet = 15,
    Color = 16,
    File = 17,
    Reference = 18,
    Folder = 19,
    EnumMember = 20,
    Constant = 21,
    Struct = 22,
    Event = 23,
    Operator = 24,
    TypeParameter = 25
}
