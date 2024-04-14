using Xunit;
using System.Reflection;

namespace DotRush.Roslyn.Tests;

[Collection("Sequential")]
public abstract class TestFixtureBase {
    protected string MockProjectsDirectory { get; set; }
    protected string MockProjectName { get; set; } = "TestProject";

    protected TestFixtureBase() {
        var assemblyLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";
        MockProjectsDirectory = Path.GetFullPath(Path.Combine(assemblyLocation, "MockData"));
    }

    protected string CreateProject(string csprojContent, string? projectName = null, string? projectDirectory = null) {
        projectName ??= MockProjectName;
        projectDirectory ??= MockProjectsDirectory;

        string csprojPath = Path.Combine(projectDirectory, projectName, $"{projectName}.csproj");
        Directory.CreateDirectory(Path.Combine(projectDirectory, projectName));

        using var writer = File.CreateText(csprojPath);
        writer.WriteLine(csprojContent);

        return csprojPath;
    }
    protected string CreateDocument(string projectPath, string documentPath, string documentContent) {
        var projectDirectory = Path.GetDirectoryName(projectPath)!;
        var documentFullPath = Path.Combine(projectDirectory, documentPath);
        File.WriteAllText(documentFullPath, documentContent);
        return documentFullPath;
    }
    protected void DeleteMockData() {
        Directory.Delete(MockProjectsDirectory, true);
    }
}