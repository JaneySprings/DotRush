using NUnit.Framework;

namespace DotRush.Roslyn.Workspaces.Tests;

[TestFixture]
public abstract class TestFixture {
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
        return CreateProject(name, new string[] { tfm }, outputType);
    }
    protected string CreateProject(string name, string[] tfm, string outputType) {
        if (tfm.Length == 0)
            throw new ArgumentException("At least one target framework must be specified", nameof(tfm));

        var projectDirectory = Path.Combine(SandboxDirectory, name);
        Directory.CreateDirectory(projectDirectory);

        var projectFile = Path.Combine(projectDirectory, $"{name}.csproj");
        var projectContent = $@"<Project Sdk=""Microsoft.NET.Sdk"">
            <PropertyGroup>
                <OutputType>{outputType}</OutputType>
                <TargetFrameworks>{string.Join(";", tfm)}</TargetFrameworks>
                <ImplicitUsings>enable</ImplicitUsings>
                <Nullable>enable</Nullable>
            </PropertyGroup>
        </Project>";

        File.WriteAllText(projectFile, projectContent);
        return projectFile;
    }

    protected string CreateFileInProject(string project, string name, string content) {
        if (project.EndsWith(".csproj"))
            project = Path.GetDirectoryName(project)!;
        
        var file = Path.Combine(project, name);
        File.WriteAllText(file, content);
        return file;
    }
}