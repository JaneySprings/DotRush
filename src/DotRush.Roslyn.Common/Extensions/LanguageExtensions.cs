namespace DotRush.Roslyn.Common.Extensions;

public static class LanguageExtensions {
    public static bool IsSourceCodeDocument(string filePath) {
        var allowedExtensions = new[] { ".cs", /* .fs .vb */};
        return allowedExtensions.Any(it => Path.GetExtension(filePath).Equals(it, StringComparison.OrdinalIgnoreCase));
    }
    public static bool IsAdditionalDocument(string filePath) {
        var allowedExtensions = new[] { ".xaml", /* maybe '.razor' ? */};
        return allowedExtensions.Any(it => Path.GetExtension(filePath).Equals(it, StringComparison.OrdinalIgnoreCase));
    }
    public static bool IsProjectFile(string filePath) {
        var allowedExtensions = new[] { ".csproj", /* fsproj vbproj */};
        return allowedExtensions.Any(it => Path.GetExtension(filePath).Equals(it, StringComparison.OrdinalIgnoreCase));
    }
    public static bool IsCompilerGeneratedFile(string filePath) {
        return filePath.EndsWith(".sg.cs", StringComparison.OrdinalIgnoreCase);
    }
}