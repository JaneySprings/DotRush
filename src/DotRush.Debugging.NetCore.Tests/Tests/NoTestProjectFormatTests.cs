using DotRush.Debugging.NetCore.Testing.Explorer;
using NUnit.Framework;

namespace DotRush.Debugging.NetCore.Tests;

public class NoTestProjectFormatTests : TestFixture {
    private TestExplorer TestExplorer = null!;

    public NoTestProjectFormatTests() : base("NoTestProject") { 
        TestProjectFileContent = $@"<Project Sdk=""Microsoft.NET.Sdk"">
            <PropertyGroup>
                <OutputType>Exe</OutputType>
                <TargetFramework>net8.0</TargetFramework>
            </PropertyGroup>

            <ItemGroup>
                <PackageReference Include=""MyPackage"" />
            </ItemGroup>
        </Project>";
    }

    [SetUp]
    public void SetUp() {
        TestExplorer = new TestExplorer();
    }

    [Test]
    public void ProjectFilesShouldBeSkippedTest() {
        CreateFileInProject("SingleFixture.cs", @"namespace TestProject;
        [TestFixture]
        public class MyFixture {
            [Test]
            public void MyTest() {}
        }
        ");
        var fixtures = TestExplorer.DiscoverTests(TestProjectFilePath);
        Assert.That(fixtures, Is.Empty);
    }
}