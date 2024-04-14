namespace DotRush.Roslyn.Tests.WorkspaceTests;

public abstract class MSBuildTestFixture : TestFixtureBase {
    protected static string SingleTargetFramework => "net8.0";
    protected static string MultiTargetFramework => "net6.0;net8.0";

    protected string CreateConsoleApp(string? projectName = null, string? targetFramework = null, string? projectDirectory = null) {
        targetFramework ??= SingleTargetFramework;
        projectName ??= MockProjectName;

        var projectPath = CreateProject(@$"
<Project Sdk=""Microsoft.NET.Sdk"">
    <PropertyGroup>
        <OutputType>Exe</OutputType>
        <TargetFrameworks>{targetFramework}</TargetFrameworks>
    </PropertyGroup>
</Project>
        ", projectName, projectDirectory);

        CreateDocument(projectPath, "Program.cs", @$"
using System;

namespace {projectName} {{
    class Program {{
        static void Main(string[] args) {{
            Console.WriteLine(""Hello World!"");
        }}
    }}
}}
        ");
        return projectPath;
    }
    protected string CreateClassLib(string? projectName = null, string? targetFramework = null, string? projectDirectory = null) {
        targetFramework ??= SingleTargetFramework;
        projectName ??= MockProjectName;

        var projectPath = CreateProject(@$"
<Project Sdk=""Microsoft.NET.Sdk"">
    <PropertyGroup>
        <OutputType>Library</OutputType>
        <TargetFrameworks>{targetFramework}</TargetFrameworks>
    </PropertyGroup>
</Project>
        ", projectName, projectDirectory);

        CreateDocument(projectPath, "Class1.cs", @$"
using System;

namespace {projectName} {{
    class Class1 {{
        static void Print() {{
            Console.WriteLine(""Hello World!"");
        }}
    }}
}}
        ");
        return projectPath;
    }
}