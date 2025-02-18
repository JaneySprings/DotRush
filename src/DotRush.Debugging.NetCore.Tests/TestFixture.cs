using NUnit.Framework;

namespace DotRush.Debugging.NetCore.Tests;

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

        <ItemGroup Condition=""'$(TargetFramework)' == 'net8.0'"">
            <PackageReference Include=""Microsoft.NET.Test.Sdk"" Version=""16.9.4"" />
            <PackageReference Include=""NUnit"" Version=""3.13.2"" />
            <PackageReference Include=""NUnit3TestAdapter"" Version=""4.0.0"" />
        </ItemGroup>
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
    protected void CreateFileInProject(string fileName, string fileContent) {
        var filePath = Path.Combine(TestProjectPath, fileName);
        File.WriteAllText(filePath, fileContent);
    }
}