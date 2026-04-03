using DotRush.Roslyn.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis;
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
        Assert.That(diagnostics[0].ProjectId, Is.EqualTo(project.Id));
    }
}
