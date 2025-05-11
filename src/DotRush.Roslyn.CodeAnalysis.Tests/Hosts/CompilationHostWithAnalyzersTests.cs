using DotRush.Common.Extensions;
using DotRush.Roslyn.CodeAnalysis.Components;
using DotRush.Roslyn.CodeAnalysis.Diagnostics;
using DotRush.Roslyn.Workspaces.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using NUnit.Framework;

namespace DotRush.Roslyn.CodeAnalysis.Tests;

public class CompilationHostWithAnalyzersTests : WorkspaceTestFixture, IAdditionalComponentsProvider {
    private CompilationHost compilationHost = null!;
    private string testDocumentPath = null!;

    protected override string CreateProject(string name, string tfm, string outputType) {
        return base.CreateProject("TestProjectCH", MultiTFM, "Library");
    }

    private async Task<List<DiagnosticContext>> GetDiagnostics(IEnumerable<Document> documents, AnalysisScope scope) {
        await compilationHost.AnalyzeAsync(documents, scope, scope, CancellationToken.None);
        return compilationHost.GetDiagnostics().SelectMany(d => d.Value).ToList();
    }
    private IEnumerable<Document> UpdateDocument(string content) {
        Workspace!.UpdateDocument(testDocumentPath, content);
        return Workspace!.Solution!.GetDocumentIdsWithFilePath(testDocumentPath).Select(id => Workspace.Solution.GetDocument(id))!;
    }

    [SetUp]
    public void SetUp() {
        compilationHost = new CompilationHost(this);
        testDocumentPath = CreateFileInProject("Main.cs", "namespace TestProjectCH { class Main { static void Main() { } } }");
        Workspace!.CreateDocument(testDocumentPath);
    }

    [Test]
    public async Task DiagnoseMultitargetProjectWithProjectScopeTest() {
        var documents = UpdateDocument(@"
using System.Diagnostics.Contracts;
namespace TestProjectCH;
class Program {
    static void Main() {
        int t = 1;
        Console.WriteLine(""Hello, World!"");
    }
}
");
        var diagnostics = await GetDiagnostics(documents, AnalysisScope.Project);
        Assert.That(diagnostics, Is.Not.Empty);
        foreach (var tfm in MultiTFM.Split(';'))
            Assert.That(diagnostics.Where(d => d.SourceName.Contains(tfm)).Count(), Is.EqualTo(23));
        Assert.That(diagnostics.Any(d => !File.Exists(d.FilePath)), Is.False, "Diagnostics should contain file paths");

        var currentFileDiagnostics = diagnostics.Where(d => PathExtensions.Equals(d.FilePath, testDocumentPath)).ToList();
        Assert.That(currentFileDiagnostics, Is.Not.Empty);
        Assert.That(currentFileDiagnostics, Has.Count.EqualTo(22));
        Assert.That(currentFileDiagnostics.Where(d => d.Diagnostic.Id == "CS8019").ToList(), Has.Count.EqualTo(2), "Diagnostics should contain 2 unnecessary usings");
        Assert.That(currentFileDiagnostics.Where(d => d.Diagnostic.Id == "CS0219").ToList(), Has.Count.EqualTo(2), "Diagnostics should contain 2 unused variables");
        Assert.That(currentFileDiagnostics.Where(d => d.Diagnostic.Id == "IDE0040").ToList(), Has.Count.EqualTo(4), "Diagnostics should contain 4 accessibility modifiers");
        Assert.That(currentFileDiagnostics.Where(d => d.Diagnostic.Id == "IDE0059").ToList(), Has.Count.EqualTo(2), "Diagnostics should contain 2 unnecessary assignments");
        Assert.That(currentFileDiagnostics.Where(d => d.Diagnostic.Id == "IDE0005").ToList(), Has.Count.EqualTo(2), "Diagnostics should contain 2 unnecessary usings");
        Assert.That(currentFileDiagnostics.Where(d => d.Diagnostic.Id == "CA1852").ToList(), Has.Count.EqualTo(2), "Diagnostics should contain 2 make class sealed");
    }
    [Test]
    public async Task DiagnoseMultitargetProjectWithDocumentScopeTest() {
        var documents = UpdateDocument(@"
using System.Diagnostics.Contracts;
namespace TestProjectCH;
class Program {
    static void Main() {
        int t = 1;
        Console.WriteLine(""Hello, World!"");
    }
}
");
        var diagnostics = await GetDiagnostics(documents, AnalysisScope.Document);
        Assert.That(diagnostics, Is.Not.Empty);
        foreach (var tfm in MultiTFM.Split(';'))
            Assert.That(diagnostics.Where(d => d.SourceName.Contains(tfm)).Count(), Is.EqualTo(10));
        Assert.That(diagnostics.Any(d => !File.Exists(d.FilePath)), Is.False, "Diagnostics should contain file paths");

        var currentFileDiagnostics = diagnostics.Where(d => PathExtensions.Equals(d.FilePath, testDocumentPath)).ToList();
        Assert.That(currentFileDiagnostics, Is.Not.Empty);
        Assert.That(currentFileDiagnostics, Has.Count.EqualTo(20));
        Assert.That(currentFileDiagnostics.Where(d => d.Diagnostic.Id == "CS8019").ToList(), Has.Count.EqualTo(2), "Diagnostics should contain 2 unnecessary usings");
        Assert.That(currentFileDiagnostics.Where(d => d.Diagnostic.Id == "CS0219").ToList(), Has.Count.EqualTo(2), "Diagnostics should contain 2 unused variables");
        Assert.That(currentFileDiagnostics.Where(d => d.Diagnostic.Id == "IDE0040").ToList(), Has.Count.EqualTo(4), "Diagnostics should contain 4 accessibility modifiers");
        Assert.That(currentFileDiagnostics.Where(d => d.Diagnostic.Id == "IDE0059").ToList(), Has.Count.EqualTo(2), "Diagnostics should contain 2 unnecessary assignments");
        Assert.That(currentFileDiagnostics.Where(d => d.Diagnostic.Id == "IDE0005").ToList(), Has.Count.EqualTo(2), "Diagnostics should contain 2 unnecessary usings");
        Assert.That(currentFileDiagnostics.Where(d => d.Diagnostic.Id == "CA1852").ToList(), Has.Count.EqualTo(0), "Some diagnostics should not be present in document scope");
    }
    [Test]
    public async Task DiagnoseMultitargetProjectWithNoneScopeTest() {
        var documents = UpdateDocument(@"
using System.Diagnostics.Contracts;
namespace TestProjectCH;
class Program {
    static void Main() {
        int t = 1;
        Console.WriteLine(""Hello, World!"");
    }
}
");
        var diagnostics = await GetDiagnostics(documents, AnalysisScope.None);
        Assert.That(diagnostics, Is.Empty);
    }

    [Test]
    public async Task GetDiagnosticsByDocumentSpanTest() {
        var documents = UpdateDocument(@"
using System.Diagnostics.Contracts;
namespace TestProjectCH;
class Program {
    static void Main() {
        int t = 1;
        Console.WriteLine(""Hello, World!"");
    }
}
");
        _ = await GetDiagnostics(documents, AnalysisScope.Project);
        var currentFileDiagnostics = compilationHost.GetDiagnosticsByDocumentSpan(documents.First(), new TextSpan(2, 5));
        Assert.That(currentFileDiagnostics, Is.Not.Empty);
        //TODO: Maybe we need to return diagnostics only for the current document?
        Assert.That(currentFileDiagnostics.Where(d => d.Diagnostic.Id == "CS8019").ToList(), Has.Count.EqualTo(2), "Diagnostics should contain unnecessary using directive");
        Assert.That(currentFileDiagnostics.Where(d => d.Diagnostic.Id == "IDE0005").ToList(), Has.Count.EqualTo(2), "Diagnostics should contain unnecessary using directive");
    }
    [Test]
    public async Task DiagnosticsShouldNotOverwriteOtherDiagnosticsTest() {
        var documents = UpdateDocument(@"
using System.Diagnostics.Contracts;
namespace TestProjectCH;
class Program {
    static void Main() {
        int t = 1;
    }
}
");
        var file2 = CreateFileInProject("File2.cs", "namespace TestProjectCH { class File2 { static void Main() { int j = 2; } } }");
        Workspace!.CreateDocument(file2);
        var documents2 = Workspace!.Solution!.GetDocumentIdsWithFilePath(file2).Select(id => Workspace.Solution.GetDocument(id))!;
        Assert.That(documents2, Is.Not.Empty);

        var diagnostics = await GetDiagnostics(documents, AnalysisScope.Document);
        Assert.That(diagnostics.Where(d => d.Diagnostic.Id == "CS0219").ToList(), Has.Count.EqualTo(2), "Diagnostics should contain 2 unused variables");
        Assert.That(diagnostics.Where(d => d.Diagnostic.Id == "IDE0059").ToList(), Has.Count.EqualTo(2), "Diagnostics should contain 2 unnecessary assignments");
        diagnostics = await GetDiagnostics(documents2!, AnalysisScope.Project);
        Assert.That(diagnostics.Where(d => d.Diagnostic.Id == "CS0219").ToList(), Has.Count.EqualTo(4), "Diagnostics should contain 2 unused variables");
        Assert.That(diagnostics.Where(d => d.Diagnostic.Id == "IDE0059").ToList(), Has.Count.EqualTo(4), "Diagnostics should contain 4 unnecessary assignments");
    }
    [Test]
    public async Task DiagnoseAndFixSomeIssuesTest() {
        var documents = UpdateDocument(@"
using System.Diagnostics.Contracts;
namespace TestProjectCH;
class Program {
    static void Main() {
        int t = 1;
        Console.WriteLine(""Hello, World!"");
    }
}
");
        var diagnostics = await GetDiagnostics(documents, AnalysisScope.Document);
        Assert.That(diagnostics, Is.Not.Empty);
        Assert.That(diagnostics.Where(d => d.Diagnostic.Id == "CS0219").ToList(), Has.Count.EqualTo(2));
        Assert.That(diagnostics.Where(d => d.Diagnostic.Id == "IDE0059").ToList(), Has.Count.EqualTo(2));

        documents = UpdateDocument(@"
namespace TestProjectCH;
class Program {
    static void Main() {
        Console.WriteLine(""Hello, World!"");
    }
}
");
        diagnostics = await GetDiagnostics(documents, AnalysisScope.Document);
        Assert.That(diagnostics, Is.Not.Empty);
        Assert.That(diagnostics.Where(d => d.Diagnostic.Id == "CS0219").ToList(), Has.Count.EqualTo(0));
        Assert.That(diagnostics.Where(d => d.Diagnostic.Id == "IDE0059").ToList(), Has.Count.EqualTo(0));
    }
    [Test]
    public async Task DiagnosticsShouldBeEmptyAfterRemovingDocumentTest() {
        var testFile = CreateFileInProject("File3.cs", @"
namespace TestProjectCH;
class EmptyClass {
    static void EmptyVoid() {
        var a = 1;
    }
}
");
        Workspace!.CreateDocument(testFile);
        var documents = Workspace!.Solution!.GetDocumentIdsWithFilePathV2(testFile).Select(id => Workspace.Solution!.GetDocument(id))!;
        _ = await GetDiagnostics(documents!, AnalysisScope.Project);
        var diagnostics = compilationHost.GetDiagnostics();
        Assert.That(diagnostics[testFile], Is.Not.Empty);

        Workspace.DeleteDocument(testFile);
        documents = Workspace!.Solution!.GetDocumentIdsWithFilePathV2(testDocumentPath).Select(id => Workspace.Solution!.GetDocument(id))!;
        _ = await GetDiagnostics(documents!, AnalysisScope.Project);
        diagnostics = compilationHost.GetDiagnostics();
        Assert.That(diagnostics[testFile], Is.Empty, "Diagnostics should be empty after removing document");
    }


    bool IAdditionalComponentsProvider.IsEnabled => false;
    IEnumerable<string> IAdditionalComponentsProvider.GetAdditionalAssemblies() {
        throw new NotImplementedException("This method is not implemented in the test project");
    }
}