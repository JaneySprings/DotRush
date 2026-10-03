using System.Text.Json;
using System.Text.Json.Serialization;
using DotRush.Common;

namespace DotRush.Protocol.Models;

[JsonConverter(typeof(DocumentUriConverter))]
public readonly record struct DocumentUri(Uri Uri) {
    public string FileSystemPath {
        get {
            if (Uri.IsUnc)
                return Uri.LocalPath;

            var filePath = Uri.UnescapeDataString(Uri.AbsolutePath);
            if (RuntimeInfo.IsWindows)
                filePath = filePath.TrimStart('/').Replace('/', '\\');

            return filePath;
        }
    }

    public static implicit operator DocumentUri(Uri uri) {
        return new DocumentUri(uri);
    }
    public static implicit operator DocumentUri(string uri) {
        return new DocumentUri(new Uri(uri));
    }
}

internal class DocumentUriConverter : JsonConverter<DocumentUri> {
    public override DocumentUri Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        return Parse(reader.GetString());
    }
    public override void Write(Utf8JsonWriter writer, DocumentUri value, JsonSerializerOptions options) {
        writer.WriteStringValue(value.Uri.AbsoluteUri);
    }
    public override DocumentUri ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        return Parse(reader.GetString());
    }
    public override void WriteAsPropertyName(Utf8JsonWriter writer, DocumentUri value, JsonSerializerOptions options) {
        writer.WritePropertyName(value.Uri.AbsoluteUri);
    }

    private static DocumentUri Parse(string? value) {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
            throw new JsonException($"Invalid document uri: '{value}'");

        return new DocumentUri(uri);
    }
}
