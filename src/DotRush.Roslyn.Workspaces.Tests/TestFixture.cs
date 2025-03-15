using System.Text;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using NUnit.Framework;
using FileSystemExtensions = DotRush.Common.Extensions.FileSystemExtensions;

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
        FileSystemExtensions.TryDeleteDirectory(SandboxDirectory);
        Directory.CreateDirectory(SandboxDirectory);
    }
    [TearDown]
    public void TearDown() {
        FileSystemExtensions.TryDeleteDirectory(SandboxDirectory);
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
            File.AppendAllText(solutionFile, $"\nProject(\"{{9A19103F-16F7-4668-BE54-9A1E7A4F7556}}\") = \"{Path.GetFileNameWithoutExtension(project)}\", \"{project}\", \"{{{Guid.NewGuid}}}\"\nEndProject");

        return solutionFile;
    }
    protected string CreateSolutionX(string name, params string[] projects) {
        var solutionFile = Path.Combine(SandboxDirectory, $"{name}.slnx");
        var solutionContent = new StringBuilder();
        solutionContent.AppendLine("<Solution>");
        foreach (var project in projects)
            solutionContent.AppendLine($"  <Project Path=\"{project}\" />");
        solutionContent.AppendLine("</Solution>");
        
        File.WriteAllText(solutionFile, solutionContent.ToString());
        return solutionFile;
    }
    protected string CreateSolutionFilter(string solutionPath, params string[] projects) {
        var solutionDirectory = Path.GetDirectoryName(solutionPath)!;
        var solutionFilterFile = Path.Combine(solutionDirectory, $"{Path.GetFileNameWithoutExtension(solutionPath)}.slnf");
        var slnFilterObject = new {
            solution = new {
                path = solutionPath,
                projects = projects
            }
        };

        File.WriteAllText(solutionFilterFile, JsonSerializer.Serialize(slnFilterObject));
        return solutionFilterFile;
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