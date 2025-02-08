using NUnit.Framework;

namespace DotRush.Essentials.TestExplorer.Tests;

[TestFixture]
public class TestFixture {
    protected string TestProjectName { get; set; }
    protected string TestProjectPath { get; set; }
    protected string TestProjectFilePath => Path.Combine(TestProjectPath, $"{TestProjectName}.csproj");
    protected string TestProjectFileContent { get; set; } = @"<Project Sdk=""Microsoft.NET.Sdk"">
        <PropertyGroup>
            <OutputType>Exe</OutputType>
            <TargetFramework>net8.0</TargetFramework>
        </PropertyGroup>
    </Project>";

    public TestFixture(string testProjectName = "SomeProject") {
        TestProjectName = testProjectName;
        TestProjectPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, TestProjectName);
    }

    [SetUp]
    public void Setup() {
        if (Directory.Exists(TestProjectPath))
            Directory.Delete(TestProjectPath, true);
        
        Directory.CreateDirectory(TestProjectPath);
        CreateProject(TestProjectFilePath);
    }

    [TearDown]
    public void TearDown() {
        if (Directory.Exists(TestProjectPath))
            Directory.Delete(TestProjectPath, true);
    }

    protected void CreateProject(string projectFilePath) {
        File.WriteAllText(projectFilePath, TestProjectFileContent);
    }
    protected void CreateProjectFile(string fileName, string fileContent) {
        var filePath = Path.Combine(TestProjectPath, fileName);
        File.WriteAllText(filePath, fileContent);
    }
}