using DotRush.Common.Extensions;
using NUnit.Framework;

namespace DotRush.Roslyn.CodeAnalysis.Tests;

public abstract class WorkspaceTestFixture : TestFixture {
    protected string TestProjectPath { get; private set; } = null!;
    protected string SandboxDirectory { get; private set; }
    protected TestWorkspace? Workspace { get; private set; }

    public WorkspaceTestFixture() {
        SandboxDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Sandbox");
    }

    [OneTimeSetUp]
    public async Task GlobalSetup() {
        FileSystemExtensions.TryDeleteDirectory(SandboxDirectory);
        Directory.CreateDirectory(SandboxDirectory);

        TestProjectPath = CreateProject(string.Empty, string.Empty, string.Empty);
        Workspace = new TestWorkspace();
        await Workspace.LoadAsync(new[] { TestProjectPath }, CancellationToken.None);
    }

    [OneTimeTearDown]
    public void GlobalTearDown() {
        FileSystemExtensions.TryDeleteDirectory(SandboxDirectory);
        Workspace = null;
    }

    protected virtual string CreateProject(string name, string tfm, string outputType) {
        ArgumentException.ThrowIfNullOrEmpty(name, nameof(name));
        ArgumentException.ThrowIfNullOrEmpty(tfm, nameof(tfm));
        ArgumentException.ThrowIfNullOrEmpty(outputType, nameof(outputType));

        var projectDirectory = Path.Combine(SandboxDirectory, name);
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
    protected string CreateFileInProject(string fileName, string content) {
        var project = Path.GetDirectoryName(TestProjectPath)!;
        var file = Path.Combine(project, fileName);
        var directory = Path.GetDirectoryName(file);
        if (!Directory.Exists(directory))
            Directory.CreateDirectory(directory!);

        File.WriteAllText(file, content);
        return file;
    }
}