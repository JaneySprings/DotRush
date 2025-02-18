using System.Collections.ObjectModel;
using DotRush.Roslyn.CodeAnalysis;
using DotRush.Roslyn.Tests.Extensions;
using DotRush.Roslyn.Tests.WorkspaceTests;
using Microsoft.CodeAnalysis;
using Xunit;

namespace DotRush.Roslyn.Tests.CodeAnalysisTests;

public class CompilationHostTests : TestFixtureBase, IDisposable {
    private readonly CompilationHost compilationHost;
    private readonly Dictionary<string, ReadOnlyCollection<Diagnostic>?> diagnostics;

    public CompilationHostTests() {
        diagnostics = new Dictionary<string, ReadOnlyCollection<Diagnostic>?>();
        compilationHost = new CompilationHost();
        compilationHost.DiagnosticsChanged += (sender, args) => {
            diagnostics[args.FilePath] = args.Diagnostics;
        };
    }

    [Fact]
    public async Task SingleTargetDocumentDiagnosticsTest() {
        var projectPath = TestProjectExtensions.CreateClassLib("MyClassLib");
        var documentPath = TestProjectExtensions.CreateDocument(Path.Combine(TestProjectExtensions.TestProjectsDirectory, "MyClassLib", "TestFile.cs"), @"
using System;

namespace MyClassLib {
    public class TestClass {
        public void TestMethod() {
            var counter = 0;
            var test = new FakeClass();
        }
    }
}
        ");
        var workspace = new TestWorkspace([projectPath]);

        await workspace.LoadSolutionAsync(CancellationToken.None).ConfigureAwait(false);
        await compilationHost.DiagnoseAsync(workspace.Solution!.Projects, CancellationToken.None).ConfigureAwait(false);

        Assert.NotEmpty(workspace.GetDocumentIdsWithFilePath(documentPath));
        Assert.NotNull(diagnostics);
        Assert.NotEmpty(diagnostics);

        var hidden = diagnostics[documentPath]!.Where(d => d.Severity == DiagnosticSeverity.Hidden).Select(it => it.Id).ToArray();
        var warnings = diagnostics[documentPath]!.Where(d => d.Severity == DiagnosticSeverity.Warning).Select(it => it.Id).ToArray();
        var errors = diagnostics[documentPath]!.Where(d => d.Severity == DiagnosticSeverity.Error).Select(it => it.Id).ToArray();

        Assert.NotEmpty(hidden);
        Assert.Single(hidden);
        Assert.Contains("CS8019", hidden);
        Assert.NotEmpty(warnings);
        Assert.Equal(2, warnings.Length);
        Assert.Contains("CA1822", warnings);
        Assert.Contains("CS0219", warnings);
        Assert.NotEmpty(errors);
        Assert.Single(errors);
        Assert.Contains("CS0246", errors);

        var documentPath2 = TestProjectExtensions.CreateDocument(Path.Combine(TestProjectExtensions.TestProjectsDirectory, "MyClassLib", "TestFile2.cs"), @"
using System;

namespace MyClassLib {
    public class TestClass2 {
        public void TestMethod() {
            var counter = 0;
            var test = new FakeClass();
        }
    }
}
        ");
        workspace.CreateDocument(documentPath2);
        Assert.NotEmpty(workspace.GetDocumentIdsWithFilePath(documentPath2));

        await compilationHost.DiagnoseAsync(workspace.Solution!.Projects, CancellationToken.None).ConfigureAwait(false);

        Assert.NotNull(diagnostics);
        Assert.NotEmpty(diagnostics);

        hidden = diagnostics[documentPath]!.Where(d => d.Severity == DiagnosticSeverity.Hidden).Select(it => it.Id).ToArray();
        warnings = diagnostics[documentPath]!.Where(d => d.Severity == DiagnosticSeverity.Warning).Select(it => it.Id).ToArray();
        errors = diagnostics[documentPath]!.Where(d => d.Severity == DiagnosticSeverity.Error).Select(it => it.Id).ToArray();
        Assert.NotEmpty(hidden);
        Assert.Single(hidden);
        Assert.Contains("CS8019", hidden);
        Assert.NotEmpty(warnings);
        Assert.Equal(2, warnings.Length);
        Assert.Contains("CA1822", warnings);
        Assert.Contains("CS0219", warnings);
        Assert.NotEmpty(errors);
        Assert.Single(errors);
        Assert.Contains("CS0246", errors);

        hidden = diagnostics[documentPath2]!.Where(d => d.Severity == DiagnosticSeverity.Hidden).Select(it => it.Id).ToArray();
        warnings = diagnostics[documentPath2]!.Where(d => d.Severity == DiagnosticSeverity.Warning).Select(it => it.Id).ToArray();
        errors = diagnostics[documentPath2]!.Where(d => d.Severity == DiagnosticSeverity.Error).Select(it => it.Id).ToArray();
        Assert.NotEmpty(hidden);
        Assert.Single(hidden);
        Assert.Contains("CS8019", hidden);
        Assert.NotEmpty(warnings);
        Assert.Equal(2, warnings.Length);
        Assert.Contains("CA1822", warnings);
        Assert.Contains("CS0219", warnings);
        Assert.NotEmpty(errors);
        Assert.Single(errors);
        Assert.Contains("CS0246", errors);
    }
    [Fact]
    public async Task MultitargetDocumentDiagnosticsTest() {
        var projectPath = TestProjectExtensions.CreateClassLib("MyClassLib", TestProjectExtensions.MultiTargetFramework);
        var documentPath = TestProjectExtensions.CreateDocument(Path.Combine(TestProjectExtensions.TestProjectsDirectory, "MyClassLib", "TestFile.cs"), @"
using System;

namespace MyClassLib {
    public class TestClass {
        public void TestMethod() {
            var unused0 = 0;
#if NET8_0
            var unused1 = 0;
#else
            var unused2 = 0;
#endif
        }
    }
}
        ");
        var workspace = new TestWorkspace([projectPath]);

        await workspace.LoadSolutionAsync(CancellationToken.None).ConfigureAwait(false);
        await compilationHost.DiagnoseAsync(workspace.Solution!.Projects, CancellationToken.None).ConfigureAwait(false);

        Assert.NotEmpty(workspace.GetDocumentIdsWithFilePath(documentPath));
        Assert.NotNull(diagnostics);
        Assert.NotEmpty(diagnostics);

        var hidden = diagnostics[documentPath]!.Where(d => d.Severity == DiagnosticSeverity.Hidden).Select(it => it.Id).OrderBy(it => it).ToArray();
        var warnings = diagnostics[documentPath]!.Where(d => d.Severity == DiagnosticSeverity.Warning).Select(it => it.Id).OrderBy(it => it).ToArray();
        var errors = diagnostics[documentPath]!.Where(d => d.Severity == DiagnosticSeverity.Error).Select(it => it.Id).OrderBy(it => it).ToArray();

        Assert.NotEmpty(hidden);
        Assert.Equal("CS8019", hidden.Single());
        Assert.NotEmpty(warnings);
        Assert.Equal("CA1822", warnings[0]);
        Assert.Equal("CS0219", warnings[1]);
        Assert.Equal("CS0219", warnings[2]);
        Assert.Equal("CS0219", warnings[3]);
        Assert.Empty(errors);

        var documentPath2 = TestProjectExtensions.CreateDocument(Path.Combine(TestProjectExtensions.TestProjectsDirectory, "MyClassLib", "TestFile2.cs"), @"
using System;

namespace MyClassLib {
    public class TestClass2 {
        public void TestMethod() {
            var unused0 = 0;
#if NET8_0
            var unused1 = 0;
#else
            var unused2 = 0;
#endif
        }
    }
}
        ");
        workspace.CreateDocument(documentPath2);
        Assert.NotEmpty(workspace.GetDocumentIdsWithFilePath(documentPath2));

        await compilationHost.DiagnoseAsync(workspace.Solution!.Projects, CancellationToken.None).ConfigureAwait(false);

        Assert.NotNull(diagnostics);
        Assert.NotEmpty(diagnostics);

        hidden = diagnostics[documentPath]!.Where(d => d.Severity == DiagnosticSeverity.Hidden).Select(it => it.Id).OrderBy(it => it).ToArray();
        warnings = diagnostics[documentPath]!.Where(d => d.Severity == DiagnosticSeverity.Warning).Select(it => it.Id).OrderBy(it => it).ToArray();
        errors = diagnostics[documentPath]!.Where(d => d.Severity == DiagnosticSeverity.Error).Select(it => it.Id).OrderBy(it => it).ToArray();
        Assert.NotEmpty(hidden);
        Assert.Equal("CS8019", hidden.Single());
        Assert.NotEmpty(warnings);
        Assert.Equal("CA1822", warnings[0]);
        Assert.Equal("CS0219", warnings[1]);
        Assert.Equal("CS0219", warnings[2]);
        Assert.Equal("CS0219", warnings[3]);
        Assert.Empty(errors);

        hidden = diagnostics[documentPath2]!.Where(d => d.Severity == DiagnosticSeverity.Hidden).Select(it => it.Id).OrderBy(it => it).ToArray();
        warnings = diagnostics[documentPath2]!.Where(d => d.Severity == DiagnosticSeverity.Warning).Select(it => it.Id).OrderBy(it => it).ToArray();
        errors = diagnostics[documentPath2]!.Where(d => d.Severity == DiagnosticSeverity.Error).Select(it => it.Id).OrderBy(it => it).ToArray();
        Assert.NotEmpty(hidden);
        Assert.Equal("CS8019", hidden.Single());
        Assert.NotEmpty(warnings);
        Assert.Equal("CA1822", warnings[0]);
        Assert.Equal("CS0219", warnings[1]);
        Assert.Equal("CS0219", warnings[2]);
        Assert.Equal("CS0219", warnings[3]);
        Assert.Empty(errors);
    }

    public void Dispose() {
        if (Directory.Exists(TestProjectExtensions.TestProjectsDirectory))
            Directory.Delete(TestProjectExtensions.TestProjectsDirectory, true);
    }
}

