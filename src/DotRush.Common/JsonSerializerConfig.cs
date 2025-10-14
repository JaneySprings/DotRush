using System.Text.Json;
using System.Text.Json.Serialization;

namespace DotRush.Common;

public static class JsonSerializerConfig {
    public static JsonSerializerOptions Options { get; }

    static JsonSerializerConfig() {
        Options = new JsonSerializerOptions {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() },
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
    }
}