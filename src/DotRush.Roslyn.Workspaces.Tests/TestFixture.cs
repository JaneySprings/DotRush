using Microsoft.CodeAnalysis;
using NUnit.Framework;

namespace DotRush.Roslyn.Workspaces.Tests;

[TestFixture]
public abstract class TestFixture {
    protected const string SingleTFM = "net8.0";
    protected const string MultiTFM = "net8.0;net9.0";

    protected string SandboxDirectory { get; set; }
    
    public TestFixture() {
        SandboxDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Sandbox");
    }

    [SetUp]
    public void Setup() {
        if (Directory.Exists(SandboxDirectory))
            Directory.Delete(SandboxDirectory, true);
        
        Directory.CreateDirectory(SandboxDirectory);
    }
    [TearDown]
    public void TearDown() {
        if (Directory.Exists(SandboxDirectory))
            Directory.Delete(SandboxDirectory, true);
    }

    protected string CreateProject(string name, string tfm, string outputType) {
        return CreateProject(name, Path.GetFileNameWithoutExtension(name), tfm, outputType);
    }
    protected string CreateProject(string name, string directory, string tfm, string outputType) {
        var projectDirectory = Path.Combine(SandboxDirectory, directory);
        if (!Directory.Exists(projectDirectory))
            Directory.CreateDirectory(projectDirectory);

        var projectFile = Path.Combine(projectDirectory, $"{name}.csproj");
        var projectContent = $@"<Project Sdk=""Microsoft.NET.Sdk"">
            <PropertyGroup>
                <OutputType>{outputType}</OutputType>
                <TargetFrameworks>{tfm}</TargetFrameworks>
                <ImplicitUsings>enable</ImplicitUsings>
                <Nullable>enable</Nullable>
            </PropertyGroup>
        </Project>";

        File.WriteAllText(projectFile, projectContent);
        return projectFile;
    }
    protected string CreateSolution(string name, params string[] projects) {
        var solutionFile = Path.Combine(SandboxDirectory, $"{name}.sln");
        var solutionContent = @"Microsoft Visual Studio Solution File, Format Version 12.00
# Visual Studio Version 16
VisualStudioVersion = 16.0.31105.104
MinimumVisualStudioVersion = 10.0.40219.1
Global
    GlobalSection(SolutionProperties) = preSolution
        HideSolutionNode = FALSE
    EndGlobalSection
EndGlobal";

        File.WriteAllText(solutionFile, solutionContent);

        foreach (var project in projects)
            File.AppendAllText(solutionFile, $"\nProject(\"{{{Guid.NewGuid}}}\") = \"{Path.GetFileNameWithoutExtension(project)}\", \"{project}\", \"{{{Guid.NewGuid}}}\"\nEndProject");

        return solutionFile;
    }

    protected string CreateFileInProject(string project, string name, string content) {
        if (project.EndsWith(".csproj"))
            project = Path.GetDirectoryName(project)!;
        
        var file = Path.Combine(project, name);
        var directory = Path.GetDirectoryName(file);
        if (!Directory.Exists(directory))
            Directory.CreateDirectory(directory!);

        File.WriteAllText(file, content);
        return file;
    }

    protected int GetProjectDocumentsCount(Project project) {
        return project.Documents.Count();
    }
    protected int GetProjectAdditionalDocumentsCount(Project project) {
        return project.AdditionalDocuments.Count();
    }
    protected int GetProjectFilesCount(Project project) {
        return GetProjectDocumentsCount(project) + GetProjectAdditionalDocumentsCount(project);
    }
}