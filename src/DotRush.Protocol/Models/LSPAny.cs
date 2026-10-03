using System.Text.Json;
using System.Text.Json.Serialization;

namespace DotRush.Protocol.Models;

[JsonConverter(typeof(LSPAnyConverter))]
public class LSPAny {
    public object? Value { get; }

    public LSPAny(object? value) {
        Value = value;
    }

    public static implicit operator LSPAny(string value) {
        return new LSPAny(value);
    }
    public static implicit operator LSPAny(int value) {
        return new LSPAny(value);
    }
    public static implicit operator LSPAny(bool value) {
        return new LSPAny(value);
    }
}

internal class LSPAnyConverter : JsonConverter<LSPAny> {
    public override LSPAny Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        switch (reader.TokenType) {
            case JsonTokenType.String:
                return new LSPAny(reader.GetString());
            case JsonTokenType.True:
            case JsonTokenType.False:
                return new LSPAny(reader.GetBoolean());
            case JsonTokenType.Number:
                if (reader.TryGetInt32(out var intValue))
                    return new LSPAny(intValue);
                if (reader.TryGetInt64(out var longValue))
                    return new LSPAny(longValue);
                return new LSPAny(reader.GetDouble());
        }

        return new LSPAny(JsonElement.ParseValue(ref reader));
    }
    public override void Write(Utf8JsonWriter writer, LSPAny value, JsonSerializerOptions options) {
        if (value.Value == null)
            writer.WriteNullValue();
        else
            JsonSerializer.Serialize(writer, value.Value, value.Value.GetType(), options);
    }
}
