using DotRush.Debugging.NetCore.Testing.Explorer;
using NUnit.Framework;

namespace DotRush.Debugging.NetCore.Tests;

public class MSTestProjectFormatTests : TestFixture {
    private TestExplorer TestExplorer = null!;

    public MSTestProjectFormatTests() : base("MSTestProjectFormat") {
        TestProjectFileContent = $@"<Project ToolVersion=""8.0"" xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
            <PropertyGroup>
                <OutputType>Exe</OutputType>
                <TargetFramework>net8.0</TargetFramework>
            </PropertyGroup>

            <ItemGroup>
                <PackageReference Include=""Microsoft.NET.Test.Sdk"" />
                <PackageReference Include=""Microsoft.TestFramework"" />
                <PackageReference Include=""Microsoft.TestAdapter"" />
            </ItemGroup>
        </Project>";
    }

    [SetUp]
    public void SetUp() {
        TestExplorer = new TestExplorer();
    }

    [Test]
    public void MSProjectFilesShouldBeDetected() {
        CreateFileInProject("SingleFixture.cs", @"namespace TestProject;
        [TestClass]
        public class MyFixture {
            [TestMethod]
            public void MyTest() {}
        }
        ");
        var fixtures = TestExplorer.DiscoverTests(TestProjectFilePath);
        Assert.That(fixtures, Is.Empty);
    }
}