using NUnit.Framework;

namespace DotRush.Essentials.TestExplorer.Tests;

public class NoTestProjectFormatTests : TestFixture {

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

    [Test]
    public void ProjectFilesShouldBeSkippedTest() {
        CreateProjectFile("SingleFixture.cs", @"namespace TestProject;
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