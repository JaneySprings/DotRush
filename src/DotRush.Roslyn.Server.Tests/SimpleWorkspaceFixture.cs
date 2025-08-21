using System.Text;
using System.Text.Json;
using DotRush.Common.Extensions;
using DotRush.Roslyn.Server.Services;
using NUnit.Framework;

namespace DotRush.Roslyn.Server.Tests;

[TestFixture]
public abstract class SimpleWorkspaceFixture {
    protected string SandboxDirectory { get; }

    protected WorkspaceService Workspace { get; private set; } = null!;

    protected SimpleWorkspaceFixture() {
        SandboxDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Sandbox");
    }

    protected virtual void OnSetup() { }
    protected virtual void OnTearDown() { }
    protected virtual WorkspaceService CreateInitializedWorkspace() {
        var workspace = new WorkspaceService(new ConfigurationService(null), null);
        if (!workspace.InitializeWorkspace())
            throw new InvalidOperationException("Failed to initialize workspace.");

        return workspace;
    }

    [SetUp]
    public void Setup() {
        SafeExtensions.ThrowOnExceptions = true;
        FileSystemExtensions.TryDeleteDirectory(SandboxDirectory);
        Directory.CreateDirectory(SandboxDirectory);

        Workspace = CreateInitializedWorkspace();
        OnSetup();
    }

    [TearDown]
    public void TearDown() {
        FileSystemExtensions.TryDeleteDirectory(SandboxDirectory);
        Workspace.Dispose();
        Workspace = null!;
        OnTearDown();
    }


    protected string CreateProject(string name, string targetFramework = "net10.0", string outputType = "Exe") {
        var projectDirectory = Path.Combine(SandboxDirectory, name);
        Directory.CreateDirectory(projectDirectory);

        var projectFile = Path.Combine(projectDirectory, $"{name}.csproj");
        var projectContent = $@"<Project Sdk=""Microsoft.NET.Sdk"">
            <PropertyGroup>
                <OutputType>{outputType}</OutputType>
                <TargetFrameworks>{targetFramework}</TargetFrameworks>
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

}