
namespace DotRush.Server;

public static class Configuration {
    private const string ExtensionId = "dotrush";

    public const string EnableRoslynAnalyzersId = $"{ExtensionId}:enableRoslynAnalyzers";
    public const string CustomRoslynAnalyzersPathId = $"{ExtensionId}:customRoslynAnalyzersPath";
}