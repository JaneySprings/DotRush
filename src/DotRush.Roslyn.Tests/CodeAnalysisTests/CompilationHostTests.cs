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
        await compilationHost.DiagnoseAsync(workspace.Solution!.Projects, useRoslynAnalyzers: false, CancellationToken.None).ConfigureAwait(false);

        Assert.NotEmpty(workspace.GetDocumentIdsWithFilePath(documentPath));
        Assert.NotNull(diagnostics);
        Assert.NotEmpty(diagnostics);

        var hidden = diagnostics[documentPath]!.Where(d => d.Severity == DiagnosticSeverity.Hidden).ToArray();
        var warnings = diagnostics[documentPath]!.Where(d => d.Severity == DiagnosticSeverity.Warning).ToArray();
        var errors = diagnostics[documentPath]!.Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();

        Assert.NotEmpty(hidden);
        Assert.Equal("CS8019", hidden.Single().Id);
        Assert.NotEmpty(warnings);
        Assert.Equal("CS0219", warnings.Single().Id);
        Assert.NotEmpty(errors);
        Assert.Equal("CS0246", errors.Single().Id);

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

        await compilationHost.DiagnoseAsync(workspace.Solution!.Projects, useRoslynAnalyzers: false, CancellationToken.None).ConfigureAwait(false);

        Assert.NotNull(diagnostics);
        Assert.NotEmpty(diagnostics);

        hidden = diagnostics[documentPath]!.Where(d => d.Severity == DiagnosticSeverity.Hidden).ToArray();
        warnings = diagnostics[documentPath]!.Where(d => d.Severity == DiagnosticSeverity.Warning).ToArray();
        errors = diagnostics[documentPath]!.Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();
        Assert.NotEmpty(hidden);
        Assert.Equal("CS8019", hidden.Single().Id);
        Assert.NotEmpty(warnings);
        Assert.Equal("CS0219", warnings.Single().Id);
        Assert.NotEmpty(errors);
        Assert.Equal("CS0246", errors.Single().Id);

        hidden = diagnostics[documentPath2]!.Where(d => d.Severity == DiagnosticSeverity.Hidden).ToArray();
        warnings = diagnostics[documentPath2]!.Where(d => d.Severity == DiagnosticSeverity.Warning).ToArray();
        errors = diagnostics[documentPath2]!.Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();
        Assert.NotEmpty(hidden);
        Assert.Equal("CS8019", hidden.Single().Id);
        Assert.NotEmpty(warnings);
        Assert.Equal("CS0219", warnings.Single().Id);
        Assert.NotEmpty(errors);
        Assert.Equal("CS0246", errors.Single().Id);
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
        await compilationHost.DiagnoseAsync(workspace.Solution!.Projects, false, CancellationToken.None).ConfigureAwait(false);

        Assert.NotEmpty(workspace.GetDocumentIdsWithFilePath(documentPath));
        Assert.NotNull(diagnostics);
        Assert.NotEmpty(diagnostics);

        var hidden = diagnostics[documentPath]!.Where(d => d.Severity == DiagnosticSeverity.Hidden).ToArray();
        var warnings = diagnostics[documentPath]!.Where(d => d.Severity == DiagnosticSeverity.Warning).ToArray();
        var errors = diagnostics[documentPath]!.Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();

        Assert.NotEmpty(hidden);
        Assert.Equal("CS8019", hidden.Single().Id);
        Assert.NotEmpty(warnings);
        Assert.Equal("CS0219", warnings[0].Id);
        Assert.Equal("CS0219", warnings[1].Id);
        Assert.Equal("CS0219", warnings[2].Id);
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

        await compilationHost.DiagnoseAsync(workspace.Solution!.Projects, false, CancellationToken.None).ConfigureAwait(false);

        Assert.NotNull(diagnostics);
        Assert.NotEmpty(diagnostics);

        hidden = diagnostics[documentPath]!.Where(d => d.Severity == DiagnosticSeverity.Hidden).ToArray();
        warnings = diagnostics[documentPath]!.Where(d => d.Severity == DiagnosticSeverity.Warning).ToArray();
        errors = diagnostics[documentPath]!.Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();
        Assert.NotEmpty(hidden);
        Assert.Equal("CS8019", hidden.Single().Id);
        Assert.NotEmpty(warnings);
        Assert.Equal("CS0219", warnings[0].Id);
        Assert.Equal("CS0219", warnings[1].Id);
        Assert.Equal("CS0219", warnings[2].Id);
        Assert.Empty(errors);

        hidden = diagnostics[documentPath2]!.Where(d => d.Severity == DiagnosticSeverity.Hidden).ToArray();
        warnings = diagnostics[documentPath2]!.Where(d => d.Severity == DiagnosticSeverity.Warning).ToArray();
        errors = diagnostics[documentPath2]!.Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();
        Assert.NotEmpty(hidden);
        Assert.Equal("CS8019", hidden.Single().Id);
        Assert.NotEmpty(warnings);
        Assert.Equal("CS0219", warnings[0].Id);
        Assert.Equal("CS0219", warnings[1].Id);
        Assert.Equal("CS0219", warnings[2].Id);
        Assert.Empty(errors);
    }

    public void Dispose() {
        if (Directory.Exists(TestProjectExtensions.TestProjectsDirectory))
            Directory.Delete(TestProjectExtensions.TestProjectsDirectory, true);
    }
}

