using System.Collections.ObjectModel;
using DotRush.Roslyn.CodeAnalysis;
using DotRush.Roslyn.Tests.Extensions;
using DotRush.Roslyn.Tests.WorkspaceTests;
using Microsoft.CodeAnalysis;
using Xunit;

namespace DotRush.Roslyn.Tests.CodeAnalysisTests;

public class CompilationHostTests : TestFixtureBase, IDisposable {
    private readonly CompilationHost compilationHost = new CompilationHost();
    private ReadOnlyCollection<Diagnostic>? diagnostics;

    public CompilationHostTests() {
        compilationHost.DiagnosticsChanged += (sender, args) => {
            diagnostics = args.Diagnostics;
        };
    }

    [Fact]
    public async Task SingleDocumentDiagnosticsTest() {
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
        compilationHost.OpenDocument(documentPath);
        await compilationHost.DiagnoseAsync(workspace.Solution!, false, CancellationToken.None).ConfigureAwait(false);

        Assert.NotNull(diagnostics);
        Assert.NotEmpty(diagnostics);

        var hidden = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Hidden);
        var warnings = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Warning);
        var errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error);

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
        compilationHost.OpenDocument(documentPath);
        await compilationHost.DiagnoseAsync(workspace.Solution!, false, CancellationToken.None).ConfigureAwait(false);

        Assert.NotNull(diagnostics);
        Assert.NotEmpty(diagnostics);

        var hidden = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Hidden);
        var warnings = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Warning).ToArray();
        var errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error);

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

