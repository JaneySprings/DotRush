using Microsoft.CodeAnalysis;

namespace DotRush.Roslyn.Server.Tests;

public abstract class MultitargetProjectFixture : BaseProjectTestFixture {
    protected const string TargetFrameworks = "net8.0;net10.0";

    public MultitargetProjectFixture() : base(nameof(MultitargetProjectFixture)) { }

    protected override string CreateProjectFileContent() {
        return $@"
<Project Sdk=""Microsoft.NET.Sdk"">
    <PropertyGroup>
        <TargetFrameworks>{TargetFrameworks}</TargetFrameworks>
        <ImplicitUsings>enable</ImplicitUsings>
        <Nullable>enable</Nullable>
    </PropertyGroup>
</Project>";
    }

    protected Document[] CreateAndGetDocuments(string name, string content) {
        var path = CreateDocument(name, content);
        return Workspace.Solution!.GetDocumentIdsWithFilePath(path).Select(id => Workspace.Solution.GetDocument(id)).ToArray()!;
    }
}