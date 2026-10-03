using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DotRush.Protocol.JsonRpc;

internal class JsonRpcMessage {
    public RequestId? Id { get; set; }
    public string? Method { get; set; }
    public JsonElement? Params { get; set; }
    public JsonElement? Result { get; set; }
    public ResponseError? Error { get; set; }

    [JsonIgnore] public bool IsRequest => Method != null && Id != null;
    [JsonIgnore] public bool IsNotification => Method != null && Id == null;
    [JsonIgnore] public bool IsResponse => Method == null && Id != null;
}

internal class ResponseError {
    public int Code { get; set; }
    public string Message { get; set; } = string.Empty;
    public JsonElement? Data { get; set; }
}

internal class CancelParams {
    public RequestId Id { get; set; }
}

[JsonConverter(typeof(RequestIdConverter))]
internal readonly record struct RequestId {
    public long Number { get; }
    public string? Text { get; }

    public RequestId(long number) {
        Number = number;
    }
    public RequestId(string text) {
        Text = text;
    }

    public override string ToString() {
        return Text ?? Number.ToString(CultureInfo.InvariantCulture);
    }
}

internal class RequestIdConverter : JsonConverter<RequestId> {
    public override RequestId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        if (reader.TokenType == JsonTokenType.Number)
            return new RequestId(reader.GetInt64());
        if (reader.TokenType == JsonTokenType.String)
            return new RequestId(reader.GetString()!);

        throw new JsonException($"Unexpected request id token: {reader.TokenType}");
    }
    public override void Write(Utf8JsonWriter writer, RequestId value, JsonSerializerOptions options) {
        if (value.Text != null)
            writer.WriteStringValue(value.Text);
        else
            writer.WriteNumberValue(value.Number);
    }
}
