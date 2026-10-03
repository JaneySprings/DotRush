using System.Text.Json;
using System.Text.Json.Serialization;

namespace DotRush.Protocol.Models;

public class CodeActionParams {
    public TextDocumentIdentifier TextDocument { get; set; } = null!;
    public DocumentRange Range { get; set; }
    public CodeActionContext? Context { get; set; }
}

public class CodeActionContext {
    public List<CodeActionKind>? Only { get; set; }
    public CodeActionTriggerKind? TriggerKind { get; set; }
}

public class CodeAction {
    public string Title { get; set; } = string.Empty;
    public CodeActionKind? Kind { get; set; }
    public bool? IsPreferred { get; set; }
    public WorkspaceEdit? Edit { get; set; }
    public Command? Command { get; set; }
    public LSPAny? Data { get; set; }
}

public enum CodeActionTriggerKind {
    Invoked = 1,
    Automatic = 2
}

[JsonConverter(typeof(CodeActionKindConverter))]
public readonly record struct CodeActionKind(string Value) {
    public static readonly CodeActionKind QuickFix = new CodeActionKind("quickfix");
    public static readonly CodeActionKind Refactor = new CodeActionKind("refactor");
    public static readonly CodeActionKind RefactorExtract = new CodeActionKind("refactor.extract");
    public static readonly CodeActionKind RefactorInline = new CodeActionKind("refactor.inline");
    public static readonly CodeActionKind RefactorRewrite = new CodeActionKind("refactor.rewrite");
    public static readonly CodeActionKind Source = new CodeActionKind("source");
    public static readonly CodeActionKind SourceOrganizeImports = new CodeActionKind("source.organizeImports");
    public static readonly CodeActionKind SourceFixAll = new CodeActionKind("source.fixAll");
}

internal class CodeActionKindConverter : JsonConverter<CodeActionKind> {
    public override CodeActionKind Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        return new CodeActionKind(reader.GetString() ?? string.Empty);
    }
    public override void Write(Utf8JsonWriter writer, CodeActionKind value, JsonSerializerOptions options) {
        writer.WriteStringValue(value.Value);
    }
}
