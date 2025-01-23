using DotRush.Essentials.Common.MSBuild;
using DotRush.Essentials.TestExplorer.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Range = DotRush.Essentials.TestExplorer.Models.Range;

namespace DotRush.Essentials.TestExplorer;

public static class TestExplorer {

    public static IEnumerable<TestCase> DiscoverTests(string projectFile) {
        var project = MSBuildProjectsLoader.LoadProject(projectFile);
        if (project != null && project.IsLegacyFormat)
            return Enumerable.Empty<TestCase>();

        var testProjectDirectory = Path.GetDirectoryName(projectFile)!;
        return GetFixtures(testProjectDirectory);
    }

    private static IEnumerable<TestCase> GetFixtures(string projectPath) {
        var result = new HashSet<TestCase>();
        var options = new CSharpParseOptions(preprocessorSymbols: new[] { "DEBUG", "DEBUGTEST" });
        var documentPaths = Directory.GetFiles(projectPath, "*.cs", SearchOption.AllDirectories); // TODO: Skip bin/obj folders
        foreach (var documentPath in documentPaths) {
            var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(documentPath), options);
            var root = tree.GetRoot();

            var namespaces = root.DescendantNodes().OfType<BaseNamespaceDeclarationSyntax>();
            foreach (var nspace in namespaces) {
                var fixtures = nspace.DescendantNodes().OfType<ClassDeclarationSyntax>();
                foreach (var fixture in fixtures) {
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
                        continue;

                    result.Add(new TestCase($"{nspace.Name}.{fixture.Identifier.Text}", fixture.Identifier.Text, documentPath) {
                        Range = GetRange(fixture),
                        Children = methods.Select(p => new TestCase($"{nspace.Name}.{fixture.Identifier.Text}.{p.Identifier.Text}", p.Identifier.Text, documentPath) {
                            Range = GetRange(p)
                        }).ToHashSet()
                    });
                }
            }
                        
        }
        return result;
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
