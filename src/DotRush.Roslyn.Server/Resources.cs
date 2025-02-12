using System.Text;

namespace DotRush.Roslyn.Server;

public static class Resources {
    public const string DotNetRegistrationFailed = "Failed to register .NET SDK. Make sure .NET SDK is installed or install it manually from [this link](https://dotnet.microsoft.com/download).";
    public const string MultipleSolutionsOrProjectsFound = "Found more than one project or solution file. Specify the `dotrush.roslyn.projectOrSolutionFiles` configuration property.";

    public static CompositeFormat ProjectRestoreFailedCompositeFormat { get; } = CompositeFormat.Parse("Failed to restore {0}:\n{1}");
    public static CompositeFormat ProjectRestoreCompositeFormat { get; } = CompositeFormat.Parse("Restoring {0}");
    public static CompositeFormat ProjectIndexCompositeFormat { get; } = CompositeFormat.Parse("Indexing {0}");
    public static CompositeFormat ProjectCompileCompositeFormat { get; } = CompositeFormat.Parse("Compiling {0}");
    public static string WorkspaceServiceWorkDoneToken  { get; private set; }

    static Resources() {
        WorkspaceServiceWorkDoneToken = Guid.NewGuid().ToString();
    }
}