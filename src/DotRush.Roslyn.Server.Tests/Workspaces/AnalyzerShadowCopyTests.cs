using System.Reflection;
using DotRush.Roslyn.Workspaces.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using NUnit.Framework;

namespace DotRush.Roslyn.Server.Tests.Workspaces;

[TestFixture]
public class AnalyzerShadowCopyTests {
    private string sandboxDirectory = null!;

    [SetUp]
    public void SetUp() {
        sandboxDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Sandbox", nameof(AnalyzerShadowCopyTests));
        if (Directory.Exists(sandboxDirectory))
            Directory.Delete(sandboxDirectory, true);
        Directory.CreateDirectory(sandboxDirectory);
    }
    [TearDown]
    public void TearDown() {
        if (Directory.Exists(sandboxDirectory))
            Directory.Delete(sandboxDirectory, true);
    }

    [Test]
    public void WithShadowCopiedAnalyzerReferences_ShouldKeepOriginalAssemblyWritable() {
        var analyzerPath = CreateAnalyzerAssembly("TestAnalyzer");
        var analyzerBytes = File.ReadAllBytes(analyzerPath);

        using var workspace = new AdhocWorkspace();
        var solution = workspace.CurrentSolution.AddProject(ProjectId.CreateNewId(), "TestProject", "TestProject", LanguageNames.CSharp);
        var project = solution.Projects
            .Single()
            .WithAnalyzerReferences([new AnalyzerFileReference(analyzerPath, new DirectAnalyzerAssemblyLoader())])
            .WithShadowCopiedAnalyzerReferences();

        var analyzerReference = project.AnalyzerReferences.OfType<AnalyzerFileReference>().Single();
        Assert.That(analyzerReference.FullPath, Is.Not.EqualTo(analyzerPath));
        Assert.That(File.Exists(analyzerReference.FullPath), Is.True);

        _ = analyzerReference.GetAssembly();

        Assert.DoesNotThrow(() => File.WriteAllBytes(analyzerPath, analyzerBytes));
    }

    private string CreateAnalyzerAssembly(string assemblyName) {
        var syntaxTree = CSharpSyntaxTree.ParseText("public sealed class TestAnalyzerAssembly { }");
        var references = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))!
            .Split(Path.PathSeparator)
            .Where(path => {
                var fileName = Path.GetFileName(path);
                return fileName.Equals("System.Private.CoreLib.dll", StringComparison.OrdinalIgnoreCase)
                    || fileName.Equals("System.Runtime.dll", StringComparison.OrdinalIgnoreCase)
                    || fileName.Equals("netstandard.dll", StringComparison.OrdinalIgnoreCase);
            })
            .Select(path => MetadataReference.CreateFromFile(path));

        var outputPath = Path.Combine(sandboxDirectory, $"{assemblyName}.dll");
        var compilation = CSharpCompilation.Create(assemblyName, [syntaxTree], references, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var result = compilation.Emit(outputPath);

        Assert.That(result.Success, Is.True, string.Join(Environment.NewLine, result.Diagnostics));
        return outputPath;
    }

    private sealed class DirectAnalyzerAssemblyLoader : IAnalyzerAssemblyLoader {
        public void AddDependencyLocation(string fullPath) {
        }

        public Assembly LoadFromPath(string fullPath) {
            return Assembly.LoadFrom(fullPath);
        }
    }
}