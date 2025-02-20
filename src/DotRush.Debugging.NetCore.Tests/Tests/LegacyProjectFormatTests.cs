using DotRush.Debugging.NetCore.Testing;
using NUnit.Framework;

namespace DotRush.Debugging.NetCore.Tests;

public class LegacyProjectFormatTests : TestFixture {

    public LegacyProjectFormatTests() : base("LegacyProjectFormat") { 
        TestProjectFileContent = $@"<Project ToolVersion=""8.0"" xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
            <PropertyGroup>
                <OutputType>Exe</OutputType>
                <TargetFrameworkVersion>v4.5</TargetFrameworkVersion>
            </PropertyGroup>

            <ItemGroup>
                <PackageReference Include=""Microsoft.NET.Test.Sdk"" />
                <PackageReference Include=""NUnit"" />
                <PackageReference Include=""NUnit3TestAdapter"" />
            </ItemGroup>
        </Project>";
    }

    [Test]
    public void LegacyProjectFilesShouldBeSkippedTest() {
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