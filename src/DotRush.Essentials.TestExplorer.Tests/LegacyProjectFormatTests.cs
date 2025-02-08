using NUnit.Framework;

namespace DotRush.Essentials.TestExplorer.Tests;

public class LegacyProjectFormatTests : TestFixture {

    public LegacyProjectFormatTests() : base("LegacyProjectFormat") { 
        TestProjectFileContent = $@"<Project ToolVersion=""8.0"" xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
            <PropertyGroup>
                <OutputType>Exe</OutputType>
                <TargetFrameworkVersion>v4.5</TargetFrameworkVersion>
            </PropertyGroup>
        </Project>";
    }

    [Test]
    public void LegacyProjectFilesShouldBeSkippedTest() {
        var fixtures = TestExplorer.DiscoverTests(TestProjectFilePath);
        Assert.That(fixtures, Is.Empty);
    }
}