using DotRush.Roslyn.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using NUnit.Framework;

namespace DotRush.Roslyn.Server.Tests.CodeAnalysis;

[TestFixture]
public class DiagnosticCollectionTests {
    [Test]
    public void GetDiagnosticsByProject_ShouldIncludeProjectLevelDiagnostics() {
        using var workspace = new AdhocWorkspace();
        var solution = workspace.CurrentSolution.AddProject(ProjectId.CreateNewId(), "TestProject", "TestProject", LanguageNames.CSharp);
        var project = solution.Projects.Single();
        var descriptor = new DiagnosticDescriptor("TEST0001", "Test", "Project diagnostic", "Testing", DiagnosticSeverity.Warning, true);
        var diagnostic = Diagnostic.Create(descriptor, Location.None);

        var diagnosticCollection = new DiagnosticCollection();
        diagnosticCollection.BeginUpdate();
        diagnosticCollection.AddDiagnostics(project.Id, new[] { new DiagnosticContext(diagnostic, project) });
        diagnosticCollection.EndUpdate();

        var diagnostics = diagnosticCollection.GetDiagnosticsByProject(project);

        Assert.That(diagnostics, Has.Count.EqualTo(1));
        Assert.That(diagnostics[0].Diagnostic.Id, Is.EqualTo("TEST0001"));
    }

    [Test]
    public async Task GetDiagnosticsByDocument_ShouldSeparateDocumentsWithSameFilePath() {
        using var workspace = new AdhocWorkspace();
        const string sharedFilePath = "/shared/Test.cs";
        var projectOneId = ProjectId.CreateNewId();
        var projectTwoId = ProjectId.CreateNewId();
        var solution = workspace.CurrentSolution
            .AddProject(projectOneId, "ProjectOne", "ProjectOne", LanguageNames.CSharp)
            .AddProject(projectTwoId, "ProjectTwo", "ProjectTwo", LanguageNames.CSharp);

        solution = solution.GetProject(projectOneId)!
            .AddDocument("Test.cs", SourceText.From("class Test { }"), filePath: sharedFilePath)
            .Project.Solution;
        solution = solution.GetProject(projectTwoId)!
            .AddDocument("Test.cs", SourceText.From("class Test { }"), filePath: sharedFilePath)
            .Project.Solution;

        var documents = new[] {
            solution.GetProject(projectOneId)!.Documents.Single(),
            solution.GetProject(projectTwoId)!.Documents.Single()
        };
        var descriptorOne = new DiagnosticDescriptor("TEST0001", "Test", "Project one diagnostic", "Testing", DiagnosticSeverity.Warning, true);
        var descriptorTwo = new DiagnosticDescriptor("TEST0002", "Test", "Project two diagnostic", "Testing", DiagnosticSeverity.Warning, true);
        var locationOne = await GetLocationAsync(documents[0]).ConfigureAwait(false);
        var locationTwo = await GetLocationAsync(documents[1]).ConfigureAwait(false);

        var diagnosticCollection = new DiagnosticCollection();
        diagnosticCollection.BeginUpdate();
        diagnosticCollection.AddDiagnostics(documents[0].Project.Id, new[] { new DiagnosticContext(Diagnostic.Create(descriptorOne, locationOne), documents[0]) });
        diagnosticCollection.AddDiagnostics(documents[1].Project.Id, new[] { new DiagnosticContext(Diagnostic.Create(descriptorTwo, locationTwo), documents[1]) });
        diagnosticCollection.EndUpdate();

        Assert.That(diagnosticCollection.GetDiagnosticsByDocument(documents[0]).Select(d => d.Diagnostic.Id), Has.Exactly(1).EqualTo("TEST0001"));
        Assert.That(diagnosticCollection.GetDiagnosticsByDocument(documents[1]).Select(d => d.Diagnostic.Id), Has.Exactly(1).EqualTo("TEST0002"));

        var diagnosticsByFile = diagnosticCollection.GetDiagnostics();
        Assert.That(diagnosticsByFile.Keys, Has.Exactly(1).EqualTo(sharedFilePath));
        Assert.That(diagnosticsByFile[sharedFilePath].Select(d => d.Diagnostic.Id).OrderBy(id => id), Is.EqualTo(new[] { "TEST0001", "TEST0002" }));
    }

    private static async Task<Location> GetLocationAsync(Document document) {
        var syntaxTree = await document.GetSyntaxTreeAsync().ConfigureAwait(false);
        Assert.That(syntaxTree, Is.Not.Null);
        return Location.Create(syntaxTree!, new TextSpan(0, 5));
    }
}
