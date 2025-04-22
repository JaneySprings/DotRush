using DotRush.Common.Extensions;
using DotRush.Roslyn.CodeAnalysis.Components;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using NUnit.Framework;

namespace DotRush.Roslyn.CodeAnalysis.Tests;

public class CompilationHostTests : WorkspaceTestFixture, IAdditionalComponentsProvider {
    private CompilationHost compilationHost = null!;
    private string mainDocumentPath = null!;
    private IEnumerable<Document> Documents => Workspace!.Solution!.GetDocumentIdsWithFilePath(mainDocumentPath).Select(id => Workspace.Solution.GetDocument(id))!;

    protected override string CreateProject(string name, string tfm, string outputType) {
        return base.CreateProject("TestProjectCH", MultiTFM, "Library");
    }

    [SetUp]
    public void SetUp() {
        compilationHost = new CompilationHost(this);
        mainDocumentPath = CreateFileInProject("Main.cs", "namespace TestProjectCH { class Main { static void Main() { } } }");
        Workspace!.CreateDocument(mainDocumentPath);
    }

    [Test]
    public async Task DiagnoseMultitargetProjectTest() {
        Workspace!.UpdateDocument(mainDocumentPath, @"
using System.Diagnostics.Contracts;
namespace TestProjectCH;
class Program {
    static void Main() {
        int t = 1;
        Console.WriteLine(""Hello, World!"");
    }
}
");
        var diagnostics = (await compilationHost.DiagnoseProjectsAsync(Documents, enableAnalyzers: false, CancellationToken.None)).SelectMany(d => d.Value).ToList();
        Assert.That(diagnostics, Is.Not.Empty);
        Assert.That(diagnostics, Has.Count.GreaterThan(4));
        Assert.That(diagnostics.Any(d => !File.Exists(d.FilePath)), Is.False, "Diagnostics should contain file paths");
        Assert.That(diagnostics.Any(d => d.IsAnalyzerDiagnostic), Is.False, "Diagnostics should not contain analyzer diagnostics");
        foreach (var tfm in MultiTFM.Split(';'))
            Assert.That(diagnostics.Any(d => d.RelatedProject.Name.Contains(tfm)), Is.True, $"Diagnostics should contain project with '{tfm}'");

        var currentFileDiagnostics = diagnostics.Where(d => PathExtensions.Equals(d.FilePath, mainDocumentPath)).ToList();
        Assert.That(currentFileDiagnostics, Is.Not.Empty);
        Assert.That(currentFileDiagnostics, Has.Count.EqualTo(4));
        Assert.That(currentFileDiagnostics.Where(d => d.Diagnostic.Id == "CS8019").ToList(), Has.Count.EqualTo(2), "Diagnostics should contain 2 unnecessary usings");
        Assert.That(currentFileDiagnostics.Where(d => d.Diagnostic.Id == "CS0219").ToList(), Has.Count.EqualTo(2), "Diagnostics should contain 2 unused variables");
    }
    [Test]
    public async Task DiagnoseMultitargetProjectWithAnalyzersTest() {
        Workspace!.UpdateDocument(mainDocumentPath, @"
using System.Diagnostics.Contracts;
namespace TestProjectCH;
class Program {
    static void Main() {
        int t = 1;
        Console.WriteLine(""Hello, World!"");
    }
}
");
        _ = await compilationHost.DiagnoseProjectsAsync(Documents, enableAnalyzers: true, CancellationToken.None);
        var diagnostics = compilationHost.GetDiagnostics().SelectMany(d => d.Value).ToList();
        Assert.That(diagnostics, Is.Not.Empty);
        Assert.That(diagnostics, Has.Count.GreaterThan(12));
        Assert.That(diagnostics.Any(d => !File.Exists(d.FilePath)), Is.False, "Diagnostics should contain file paths");
        Assert.That(diagnostics.Any(d => d.IsAnalyzerDiagnostic), Is.True, "Diagnostics should contain analyzer diagnostics");
        foreach (var tfm in MultiTFM.Split(';'))
            Assert.That(diagnostics.Any(d => d.RelatedProject.Name.Contains(tfm)), Is.True, $"Diagnostics should contain project with '{tfm}'");

        var currentFileDiagnostics = diagnostics.Where(d => PathExtensions.Equals(d.FilePath, mainDocumentPath)).ToList();
        Assert.That(currentFileDiagnostics, Is.Not.Empty);
        Assert.That(currentFileDiagnostics, Has.Count.EqualTo(12));
        // For both tfms
        Assert.That(currentFileDiagnostics.Where(d => d.Diagnostic.Id == "CS8019").ToList(), Has.Count.EqualTo(2), "Diagnostics should contain 2 unnecessary usings");
        Assert.That(currentFileDiagnostics.Where(d => d.Diagnostic.Id == "CS0219").ToList(), Has.Count.EqualTo(2), "Diagnostics should contain 2 unused variables");
        // For first tfm + (UnnecessaryUsingsFixable)
        Assert.That(currentFileDiagnostics.Where(d => d.Diagnostic.Id == "IDE0055").ToList(), Has.Count.EqualTo(2), "Diagnostics should contain 2 fix formatting");
        Assert.That(currentFileDiagnostics.Where(d => d.Diagnostic.Id == "IDE0040").ToList(), Has.Count.EqualTo(2), "Diagnostics should contain 2 accessibility modifiers");
        Assert.That(currentFileDiagnostics.Where(d => d.Diagnostic.Id == "IDE0059").ToList(), Has.Count.EqualTo(1), "Diagnostics should contain 1 unused value assignment");
        Assert.That(currentFileDiagnostics.Where(d => d.Diagnostic.Id == "IDE0005").ToList(), Has.Count.EqualTo(1), "Diagnostics should contain 1 unnecessary using directive");
        Assert.That(currentFileDiagnostics.Where(d => d.Diagnostic.Id == "IDE0160").ToList(), Has.Count.EqualTo(1), "Diagnostics should contain 1 convert to block scoped");
    }

    [Test]
    public async Task DiagnoseWithNoIssuesTest() {
        Workspace!.UpdateDocument(mainDocumentPath, @"
namespace TestProjectCH;
class Program 
{
    static void Main() 
    {
        Console.WriteLine(""No issues here!"");
    }
}
");
        var diagnostics = (await compilationHost.DiagnoseProjectsAsync(Documents, enableAnalyzers: false, CancellationToken.None)).SelectMany(d => d.Value).ToList();;
        var currentFileDiagnostics = diagnostics.Where(d => PathExtensions.Equals(d.FilePath, mainDocumentPath)).ToList();
        Assert.That(currentFileDiagnostics.Where(d => d.Diagnostic.Id.StartsWith("CS")), Is.Empty, "There should be no compiler diagnostics");
    }
    [Test]
    public async Task GetDiagnosticsByDocumentSpanTest() {
        Workspace!.UpdateDocument(mainDocumentPath, @"
using System.Diagnostics.Contracts;
namespace TestProjectCH;
class Program {
    static void Main() {
        int t = 1;
        Console.WriteLine(""Hello, World!"");
    }
}
");
        _ = await compilationHost.DiagnoseProjectsAsync(Documents, enableAnalyzers: false, CancellationToken.None);
        var currentFileDiagnostics = compilationHost.GetDiagnosticsByDocumentSpan(Documents.First(), new TextSpan(2, 5));
        Assert.That(currentFileDiagnostics, Is.Not.Empty);
        //TODO: Maybe we need to return diagnostics only for the current document?
        Assert.That(currentFileDiagnostics.Where(d => d.Diagnostic.Id == "CS8019").ToList(),Has.Count.EqualTo(2), "Diagnostics should contain 1 unnecessary using directive");
    }
    [Test]
    public async Task AnalyzerDiagnosticsShouldNotOverwriteOtherDiagnosticsTest() {
        Workspace!.UpdateDocument(mainDocumentPath, @"
using System.Diagnostics.Contracts;
namespace TestProjectCH;
class Program {
    static void Main() {
        int t = 1;
    }
}
");
        var file2 = CreateFileInProject("File2.cs", "namespace TestProjectCH { class File2 { static void Main() { } } }");
        Workspace!.CreateDocument(file2);
        var documents2 = Workspace!.Solution!.GetDocumentIdsWithFilePath(file2).Select(id => Workspace.Solution.GetDocument(id))!;
        Assert.That(documents2, Is.Not.Empty);

        _ = await compilationHost.DiagnoseProjectsAsync(Documents, enableAnalyzers: true, CancellationToken.None);
        _ = await compilationHost.DiagnoseProjectsAsync(documents2!, enableAnalyzers: true, CancellationToken.None);

        var currentFileDiagnostics = compilationHost.GetDiagnostics().SelectMany(d => d.Value).Where(d => PathExtensions.Equals(d.FilePath, mainDocumentPath)).ToList();
        Assert.That(currentFileDiagnostics, Is.Not.Empty);
        Assert.That(currentFileDiagnostics.Where(d => d.Diagnostic.Id == "IDE0059").ToList(), Has.Count.EqualTo(1), "Diagnostics should contain 1 unused value assignment");
    }
    [Test]
    public async Task DiagnosticsWithProjectScopeTest() {
        Workspace!.UpdateDocument(mainDocumentPath, @"
using System.Diagnostics.Contracts;
namespace TestProjectCH;
class Program {
    static void Main() {
        int t = 1;
    }
}
");
        var diagnostics = await compilationHost.DiagnoseProjectsAsync(Documents, enableAnalyzers: false, CancellationToken.None);
        Assert.That(diagnostics.Keys, Has.Count.GreaterThan(1));
        Assert.That(diagnostics.ContainsKey(mainDocumentPath), Is.True, "Diagnostics should contain test document path");
        Assert.That(diagnostics[mainDocumentPath], Is.Not.Empty);
        
        var assemblyInfoPath = diagnostics.Keys.FirstOrDefault(d => d.EndsWith("AssemblyInfo.cs"));
        Assert.That(assemblyInfoPath, Is.Not.Null, "Diagnostics should contain AssemblyInfo.cs path");
        Assert.That(diagnostics[assemblyInfoPath!], Is.Not.Empty);
    }
    [Test]
    public async Task DiagnosticsWithCurrentDocumentScopeTest() {
        Workspace!.UpdateDocument(mainDocumentPath, @"
using System.Diagnostics.Contracts;
namespace TestProjectCH;
class Program {
    static void Main() {
        int t = 1;
    }
}
");
        var diagnostics = await compilationHost.DiagnoseDocumentsAsync(Documents, enableAnalyzers: false, CancellationToken.None);
        Assert.That(diagnostics.Keys, Has.Count.EqualTo(1));
        Assert.That(diagnostics.ContainsKey(mainDocumentPath), Is.True, "Diagnostics should contain test document path");
        Assert.That(diagnostics[mainDocumentPath], Is.Not.Empty);
        
        var assemblyInfoPath = diagnostics.Keys.FirstOrDefault(d => d.EndsWith("AssemblyInfo.cs"));
        Assert.That(assemblyInfoPath, Is.Null);
    }
    [Test]
    public async Task DiagnoseAndFixAllIssuesTest() {
        Workspace!.UpdateDocument(mainDocumentPath, @"
using System.Diagnostics.Contracts;
namespace TestProjectCH;
class Program {
    static void Main() {
        int t = 1;
        Console.WriteLine(""Hello, World!"");
    }
}
");
        var diagnostics = await compilationHost.DiagnoseProjectsAsync(Documents, enableAnalyzers: false, CancellationToken.None);
        Assert.That(diagnostics[mainDocumentPath], Is.Not.Empty);
        Assert.That(diagnostics[mainDocumentPath], Has.Count.EqualTo(4));
        
        Workspace!.UpdateDocument(mainDocumentPath, @"
namespace TestProjectCH;
class Program {
    static void Main() {
        Console.WriteLine(""Hello, World!"");
    }
}
");
        diagnostics = await compilationHost.DiagnoseProjectsAsync(Documents, enableAnalyzers: false, CancellationToken.None);
        Assert.That(diagnostics[mainDocumentPath], Is.Empty);
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
        var diagnostics = await compilationHost.DiagnoseProjectsAsync(Documents, enableAnalyzers: false, CancellationToken.None);
        Assert.That(diagnostics[testFile], Is.Not.Empty);

        Workspace.DeleteDocument(testFile);
        diagnostics = await compilationHost.DiagnoseProjectsAsync(Documents, enableAnalyzers: false, CancellationToken.None);
        Assert.That(diagnostics[testFile], Is.Empty, "Diagnostics should be empty after removing document");
    }

    IEnumerable<string> IAdditionalComponentsProvider.GetAdditionalAssemblies() {
        return Enumerable.Empty<string>();
    }
}