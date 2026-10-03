using System.Text.Json.Serialization;

namespace DotRush.Protocol.Models;

public class MarkupContent {
    public MarkupKind Kind { get; set; } = MarkupKind.Markdown;
    public string Value { get; set; } = string.Empty;
}

[JsonConverter(typeof(JsonStringEnumConverter<MarkupKind>))]
public enum MarkupKind {
    [JsonStringEnumMemberName("plaintext")] PlainText,
    [JsonStringEnumMemberName("markdown")] Markdown
}
