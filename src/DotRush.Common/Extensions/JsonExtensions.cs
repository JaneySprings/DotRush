using System.Text.Json;

namespace DotRush.Common.Extensions;

public static class JsonExtensions {
    public static T? Deserialize<T>(string filePath) where T : class {
        if (!File.Exists(filePath))
            return null;

        return SafeExtensions.Invoke<T?>(null, () => JsonSerializer.Deserialize<T>(File.ReadAllText(filePath)));
    }
}