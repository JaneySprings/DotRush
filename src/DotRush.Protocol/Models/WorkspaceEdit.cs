using System.Text.Json;
using System.Text.Json.Serialization;

namespace DotRush.Protocol.Models;

public class WorkspaceEdit {
    public Dictionary<DocumentUri, List<TextEdit>>? Changes { get; set; }
    public List<IDocumentChange>? DocumentChanges { get; set; }
}

[JsonConverter(typeof(DocumentChangeConverter))]
public interface IDocumentChange { }

public class TextDocumentEdit : IDocumentChange {
    public OptionalVersionedTextDocumentIdentifier TextDocument { get; set; } = null!;
    public List<TextEdit> Edits { get; set; } = null!;
}

public record CreateFile(DocumentUri Uri, CreateFileOptions? Options, string? AnnotationId) : IDocumentChange {
    public string Kind => "create";
}
public readonly record struct CreateFileOptions(bool? Overwrite, bool? IgnoreIfExists);

public record RenameFile(DocumentUri OldUri, DocumentUri NewUri, RenameFileOptions? Options, string? AnnotationId) : IDocumentChange {
    public string Kind => "rename";
}
public readonly record struct RenameFileOptions(bool? Overwrite, bool? IgnoreIfExists);

public record DeleteFile(DocumentUri Uri, DeleteFileOptions? Options, string? AnnotationId) : IDocumentChange {
    public string Kind => "delete";
}
public readonly record struct DeleteFileOptions(bool? Recursive, bool? IgnoreIfNotExists);

internal class DocumentChangeConverter : JsonConverter<IDocumentChange> {
    public override IDocumentChange? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        var element = JsonElement.ParseValue(ref reader);
        if (!element.TryGetProperty("kind", out var kind))
            return element.Deserialize<TextDocumentEdit>(options);

        switch (kind.GetString()) {
            case "create": return element.Deserialize<CreateFile>(options);
            case "rename": return element.Deserialize<RenameFile>(options);
            case "delete": return element.Deserialize<DeleteFile>(options);
        }

        throw new JsonException($"Unknown document change kind: '{kind}'");
    }
    public override void Write(Utf8JsonWriter writer, IDocumentChange value, JsonSerializerOptions options) {
        JsonSerializer.Serialize(writer, value, value.GetType(), options);
    }
}
