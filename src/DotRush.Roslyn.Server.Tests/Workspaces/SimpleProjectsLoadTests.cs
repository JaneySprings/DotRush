using System.Xml;
using DotRush.Common;
using DotRush.Common.Interop;
using Microsoft.CodeAnalysis;
using NUnit.Framework;

namespace DotRush.Roslyn.Server.Tests;

public class SimpleProjectsLoadTests : SimpleWorkspaceFixture {
    private const string SingleTFM = "net8.0";
    private const string MultiTFM = "net8.0;net10.0";

    [Test]
    public async Task LoadSingleProjectTest() {
        var projectPath = CreateProject("MyProject", SingleTFM, "Exe");

        await Workspace.LoadAsync(new[] { projectPath }, CancellationToken.None).ConfigureAwait(false);

        Assert.That(Workspace.Solution!.Projects.Count(), Is.EqualTo(1));
        Assert.That(Workspace.Solution.Projects.First().Name, Is.EqualTo("MyProject"));
    }
    [Test]
    public async Task LoadSingleProjectsTest() {
        var project1Path = CreateProject("MyProject", SingleTFM, "Exe");
        var project2Path = CreateProject("MyProject2", SingleTFM, "Library");

        await Workspace.LoadAsync(new[] { project2Path, project1Path }, CancellationToken.None).ConfigureAwait(false);

        Assert.That(Workspace.Solution!.Projects.Count(), Is.EqualTo(2));
        Assert.That(Workspace.Solution.Projects, Has.One.Matches<Project>(p => p.Name == "MyProject2"));
        Assert.That(Workspace.Solution.Projects, Has.One.Matches<Project>(p => p.Name == "MyProject"));
    }
    [Test]
    public async Task LoadSingleProjectWithRelativePathTest() {
        var projectPath = CreateProject("MyProject", SingleTFM, "Exe");
        projectPath = Path.GetRelativePath(Directory.GetCurrentDirectory(), projectPath);

        await Workspace.LoadAsync(new[] { projectPath }, CancellationToken.None).ConfigureAwait(false);
        Assert.That(Workspace.Solution!.Projects.Count(), Is.EqualTo(1));
        Assert.That(Workspace.Solution.Projects.First().Name, Is.EqualTo("MyProject"));
    }

    [Test]
    public async Task LoadMultitargetProjectTest() {
        var projectPath = CreateProject("MyProject", MultiTFM, "Exe");

        await Workspace.LoadAsync(new[] { projectPath }, CancellationToken.None).ConfigureAwait(false);
        Assert.That(Workspace.Solution!.Projects.Count(), Is.EqualTo(2));

        var (tfm1, tfm2) = GetTFMs(MultiTFM);
        Assert.That(Workspace.Solution.Projects, Has.One.Matches<Project>(p => p.Name == $"MyProject({tfm1})"));
        Assert.That(Workspace.Solution.Projects, Has.One.Matches<Project>(p => p.Name == $"MyProject({tfm2})"));
    }
    [Test]
    public async Task LoadMultitargetProjectsTest() {
        var project1Path = CreateProject("MyProject", MultiTFM, "Exe");
        var project2Path = CreateProject("MyProject2", MultiTFM, "Library");

        await Workspace.LoadAsync(new[] { project2Path, project1Path }, CancellationToken.None).ConfigureAwait(false);
        Assert.That(Workspace.Solution!.Projects.Count(), Is.EqualTo(4));

        var (tfm1, tfm2) = GetTFMs(MultiTFM);
        Assert.That(Workspace.Solution.Projects, Has.One.Matches<Project>(p => p.Name == $"MyProject2({tfm1})"));
        Assert.That(Workspace.Solution.Projects, Has.One.Matches<Project>(p => p.Name == $"MyProject2({tfm2})"));
        Assert.That(Workspace.Solution.Projects, Has.One.Matches<Project>(p => p.Name == $"MyProject({tfm1})"));
        Assert.That(Workspace.Solution.Projects, Has.One.Matches<Project>(p => p.Name == $"MyProject({tfm2})"));
    }

    [Test]
    public void ErrorOnRestoreTest() {
        var projectPath = CreateProject("MyProject", "MyError>/<", "Exe");

        Assert.ThrowsAsync<XmlException>(async () => await Workspace.LoadAsync(new[] { projectPath }, CancellationToken.None).ConfigureAwait(false));
    }

    [Test]
    public async Task BuildProjectWithSourceGeneratorReferenceTest() {
        var generatorProjectPath = CreateSourceGeneratorProject("MySourceGenerator");
        var projectPath = CreateProjectWithSourceGeneratorReference("MyProject", SingleTFM, generatorProjectPath);

        var buildResult = BuildProject(projectPath);
        Assert.That(buildResult.Success, Is.True, $"Initial build failed:{Environment.NewLine}{buildResult.GetAllOutput()}");

        await Workspace.LoadAsync(new[] { projectPath }, CancellationToken.None);
        Assert.That(Workspace.Solution!.Projects, Has.One.Matches<Project>(p => p.Name == "MyProject"));

        var compilation = await Workspace.Solution.Projects.Single(p => p.Name == "MyProject").GetCompilationAsync();
        Assert.That(compilation?.GetTypeByMetadataName("MySourceGenerator.Generated.GeneratedClass"), Is.Not.Null);

        var generatorSourcePath = Path.Combine(Path.GetDirectoryName(generatorProjectPath)!, "TestSourceGenerator.cs");
        File.AppendAllText(generatorSourcePath, $"{Environment.NewLine}// Force the generator assembly to be recompiled");

        buildResult = BuildProject(projectPath);
        Assert.That(buildResult.Success, Is.True, $"Build failed after loading the project into the workspace:{Environment.NewLine}{buildResult.GetAllOutput()}");
    }

    private (string tfm1, string tfm2) GetTFMs(string tfm) {
        var tfms = tfm.Split(';');
        return (tfms[0], tfms[1]);
    }
    private string CreateSourceGeneratorProject(string name) {
        var projectDirectory = Path.Combine(SandboxDirectory, name);
        Directory.CreateDirectory(projectDirectory);

        var projectFile = Path.Combine(projectDirectory, $"{name}.csproj");
        var projectContent = @"<Project Sdk=""Microsoft.NET.Sdk"">
            <PropertyGroup>
                <TargetFramework>netstandard2.0</TargetFramework>
                <LangVersion>latest</LangVersion>
                <Nullable>enable</Nullable>
                <EnforceExtendedAnalyzerRules>true</EnforceExtendedAnalyzerRules>
            </PropertyGroup>
            <ItemGroup>
                <PackageReference Include=""Microsoft.CodeAnalysis.CSharp"" Version=""4.8.0"" PrivateAssets=""all"" />
            </ItemGroup>
        </Project>";
        File.WriteAllText(projectFile, projectContent);

        var generatorFile = Path.Combine(projectDirectory, "TestSourceGenerator.cs");
        var generatorContent = $@"using Microsoft.CodeAnalysis;

namespace {name};

[Generator]
public class TestSourceGenerator : IIncrementalGenerator {{
    public void Initialize(IncrementalGeneratorInitializationContext context) {{
        context.RegisterPostInitializationOutput(c => c.AddSource(""GeneratedClass.g.cs"",
            ""namespace {name}.Generated {{ public static class GeneratedClass {{ }} }}""));
    }}
}}";
        File.WriteAllText(generatorFile, generatorContent);
        return projectFile;
    }
    private string CreateProjectWithSourceGeneratorReference(string name, string targetFramework, string generatorProjectPath) {
        var projectDirectory = Path.Combine(SandboxDirectory, name);
        Directory.CreateDirectory(projectDirectory);

        var projectFile = Path.Combine(projectDirectory, $"{name}.csproj");
        var projectContent = $@"<Project Sdk=""Microsoft.NET.Sdk"">
            <PropertyGroup>
                <OutputType>Library</OutputType>
                <TargetFrameworks>{targetFramework}</TargetFrameworks>
                <ImplicitUsings>enable</ImplicitUsings>
                <Nullable>enable</Nullable>
            </PropertyGroup>
            <ItemGroup>
                <ProjectReference Include=""{generatorProjectPath}"" OutputItemType=""Analyzer"" ReferenceOutputAssembly=""false"" />
            </ItemGroup>
        </Project>";
        File.WriteAllText(projectFile, projectContent);
        return projectFile;
    }
    private ProcessResult BuildProject(string projectPath) {
        return new ProcessRunner("dotnet" + RuntimeInfo.ExecExtension, new ProcessArgumentBuilder()
            .Append("build")
            .AppendQuoted(projectPath)
            .Append("-nodeReuse:false"))
            .WaitForExit();
    }
}