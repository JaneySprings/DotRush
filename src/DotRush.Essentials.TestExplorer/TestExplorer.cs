using DotRush.Essentials.Common.Logging;
using DotRush.Essentials.Common.MSBuild;
using DotRush.Essentials.TestExplorer.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Range = DotRush.Essentials.TestExplorer.Models.Range;

namespace DotRush.Essentials.TestExplorer;

public static class TestExplorer {

    public static IEnumerable<TestFixture> DiscoverTests(string projectFile) {
        var project = MSBuildProjectsLoader.LoadProject(projectFile);
        if (project != null && project.IsLegacyFormat) {
            CurrentSessionLogger.Debug($"Project {projectFile} is in legacy format. Skipping test discovery.");
            return Enumerable.Empty<TestFixture>();
        }
        if (project != null && !project.HasPackage("NUnit") && !project.HasPackage("xunit")) {
            CurrentSessionLogger.Debug($"Project {projectFile} does not have NUnit or xUnit package. Skipping test discovery.");
            return Enumerable.Empty<TestFixture>();
        }

        var testProjectDirectory = Path.GetDirectoryName(projectFile)!;
        return GetFixtures(testProjectDirectory);
    }

    private static IEnumerable<TestFixture> GetFixtures(string projectPath) {
        var result = new Dictionary<string, TestFixture>();
        var options = new CSharpParseOptions(preprocessorSymbols: new[] { "DEBUG", "DEBUGTEST" });
        var documentPaths = Directory.GetFiles(projectPath, "*.cs", SearchOption.AllDirectories); // TODO: Skip bin/obj folders
        foreach (var documentPath in documentPaths) {
            var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(documentPath), options);
            var root = tree.GetRoot();

            var namespaces = root.DescendantNodes().OfType<BaseNamespaceDeclarationSyntax>();
            foreach (var nspace in namespaces) {
                var classes = nspace.DescendantNodes().OfType<ClassDeclarationSyntax>();
                foreach (var @class in classes) {
                    var fixtureId = $"{nspace.Name}.{@class.Identifier.Text}";
                    
                    if (!result.TryGetValue(fixtureId, out var fixture)) {
                        fixture = new TestFixture(fixtureId, @class.Identifier.Text, documentPath);
                        fixture.Range = GetRange(@class);
                        
                    }
                    
                    var testCases = GetTestCases(@class, fixtureId, documentPath);
                    if (!testCases.Any())
                        continue;

                    fixture.Children.UnionWith(testCases);
                    result.TryAdd(fixtureId, fixture);
                }
            }
                        
        }
        return result.Values;
    }
    private static IEnumerable<TestCase> GetTestCases(ClassDeclarationSyntax fixture, string fixtureId, string filePath) {
        var methods = fixture.DescendantNodes().OfType<MethodDeclarationSyntax>().Where(node => {
            // XUnit
            var hasFactAttribute = node.AttributeLists.Any(p => p.Attributes.Any(a => a.Name.ToString().EndsWith("Fact", StringComparison.InvariantCulture)));
            var hasTheoryAttribute = node.AttributeLists.Any(p => p.Attributes.Any(a => a.Name.ToString().EndsWith("Theory", StringComparison.InvariantCulture)));
            // NUnit
            var hasTestAttribute = node.AttributeLists.Any(p => p.Attributes.Any(a => a.Name.ToString().EndsWith("Test", StringComparison.InvariantCulture)));
            var hasTestCaseAttribute = node.AttributeLists.Any(p => p.Attributes.Any(a => a.Name.ToString().EndsWith("TestCase", StringComparison.InvariantCulture)));

            return hasFactAttribute || hasTheoryAttribute || hasTestAttribute || hasTestCaseAttribute;
        });

        if (!methods.Any())
            return Enumerable.Empty<TestCase>();

        return methods.Select(p => new TestCase($"{fixtureId}.{p.Identifier.Text}", p.Identifier.Text, filePath) {
            Range = GetRange(p)
        });
    }

    private static Range GetRange(SyntaxNode node) {
        return new Range {
            Start = new Position {
                Line = node.GetLocation().GetLineSpan().StartLinePosition.Line,
                Character = node.GetLocation().GetLineSpan().StartLinePosition.Character
            },
            End = new Position {
                Line = node.GetLocation().GetLineSpan().EndLinePosition.Line,
                Character = node.GetLocation().GetLineSpan().EndLinePosition.Character
            }
        };
    }
}
