using System.Text.Json;
using System.Text.Json.Serialization;

namespace DotRush.Protocol.Models;

public class TextEdit {
    public DocumentRange Range { get; set; }
    public string NewText { get; set; } = string.Empty;
}

public class InsertReplaceEdit {
    public string NewText { get; set; } = string.Empty;
    public DocumentRange Insert { get; set; }
    public DocumentRange Replace { get; set; }
}

[JsonConverter(typeof(TextEditOrInsertReplaceEditConverter))]
public class TextEditOrInsertReplaceEdit {
    public TextEdit? TextEdit { get; }
    public InsertReplaceEdit? InsertReplaceEdit { get; }

    public TextEditOrInsertReplaceEdit(TextEdit textEdit) {
        TextEdit = textEdit;
    }
    public TextEditOrInsertReplaceEdit(InsertReplaceEdit insertReplaceEdit) {
        InsertReplaceEdit = insertReplaceEdit;
    }
}

internal class TextEditOrInsertReplaceEditConverter : JsonConverter<TextEditOrInsertReplaceEdit> {
    public override TextEditOrInsertReplaceEdit Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        var element = JsonElement.ParseValue(ref reader);
        if (element.TryGetProperty("range", out _))
            return new TextEditOrInsertReplaceEdit(element.Deserialize<TextEdit>(options)!);

        return new TextEditOrInsertReplaceEdit(element.Deserialize<InsertReplaceEdit>(options)!);
    }
    public override void Write(Utf8JsonWriter writer, TextEditOrInsertReplaceEdit value, JsonSerializerOptions options) {
        if (value.TextEdit != null)
            JsonSerializer.Serialize(writer, value.TextEdit, options);
        else
            JsonSerializer.Serialize(writer, value.InsertReplaceEdit, options);
    }
}
