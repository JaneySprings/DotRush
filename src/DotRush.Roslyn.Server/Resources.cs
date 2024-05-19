using System.Text;

namespace DotRush.Roslyn.Server;

public static class Resources {
    public static CompositeFormat ProjectRestoreFailedCompositeFormat { get; } = CompositeFormat.Parse("Failed to restore {0}:\n{1}");
    public static CompositeFormat ProjectRestoreCompositeFormat { get; } = CompositeFormat.Parse("Restoring {0}");
    public static CompositeFormat ProjectIndexCompositeFormat { get; } = CompositeFormat.Parse("Indexing {0}");
    public static CompositeFormat ProjectCompileCompositeFormat { get; } = CompositeFormat.Parse("Compiling {0}");

    public const string DotNetRegistrationFailed = "Failed to register .NET SDK. Make sure .NET SDK is installed or install it manually from [this link](https://dotnet.microsoft.com/download).";
}