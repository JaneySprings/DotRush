
namespace DotRush.Roslyn.Server.Tests;

public class NUnitTestProjectFixture : BaseProjectTestFixture {
    public NUnitTestProjectFixture() : base(nameof(NUnitTestProjectFixture)) { }

    protected override string CreateProjectFileContent() {
        return @"
<Project Sdk=""Microsoft.NET.Sdk"">
    <PropertyGroup>
        <TargetFrameworks>net10.0</TargetFrameworks>
        <ImplicitUsings>enable</ImplicitUsings>
        <Nullable>enable</Nullable>
    </PropertyGroup>

    <ItemGroup>
        <PackageReference Include=""Microsoft.NET.Test.Sdk"" Version=""17.11.1"" />
        <PackageReference Include=""NUnit"" Version=""4.1.0"" />
    </ItemGroup>
</Project>";
    }

    protected string CreateUnitTestDocument(string content) {
        return CreateDocument("NUnitTestCase", $@"
using NUnit.Framework;
namespace NUnitTestProject;

{content}
");
    }
}