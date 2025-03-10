using DotRush.Common.Extensions;
using DotRush.Debugging.NetCore.Testing.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Range = DotRush.Debugging.NetCore.Testing.Models.Range;

namespace DotRush.Debugging.NetCore.Testing.Explorer;

public abstract class TestExplorerSyntaxWalker {
    private readonly string[] preprocessorSymbols = new[] { "DEBUG", "DEBUGTEST" };

    protected IEnumerable<TestFixture> GetFixtures(string projectDirectory) {
        var result = new Dictionary<string, TestFixture>();
        var options = new CSharpParseOptions(preprocessorSymbols: preprocessorSymbols);
        var documentPaths = Directory.GetFiles(projectDirectory, "*.cs", SearchOption.AllDirectories);
        foreach (var documentPath in documentPaths) {
            var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(documentPath), options);
            var root = tree.GetRoot();

            var namespaces = root.DescendantNodes().OfType<BaseNamespaceDeclarationSyntax>();
            foreach (var nspace in namespaces) {
                var classes = nspace.DescendantNodes().OfType<ClassDeclarationSyntax>();
                foreach (var klass in classes) {
                    var fixtureId = $"{nspace.Name}.{klass.Identifier.Text}";
                    if (!result.TryGetValue(fixtureId, out var fixture)) {
                        fixture = new TestFixture(fixtureId, klass.Identifier.Text, documentPath);
                        fixture.IsAbstract = klass.Modifiers.Any(p => p.IsKind(SyntaxKind.AbstractKeyword));
                        fixture.Range = GetRange(klass);
                    }

                    fixture.BaseFixtureName ??= GetBaseClassName(klass);

                    var testCases = GetTestCases(klass, fixture, documentPath);
                    fixture.TestCases.AddRange(testCases);
                    result.TryAdd(fixture.Id, fixture);
                }
            }
        }
        return result.Values;
    }
    protected IEnumerable<TestCase> GetTestCases(ClassDeclarationSyntax fixture, TestFixture parent, string documentPath) {
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

        return methods.Select(p => new TestCase($"{parent.Id}.{p.Identifier.Text}", p.Identifier.Text, documentPath) {
            Range = GetRange(p)
        });
    }
    protected Range GetRange(SyntaxNode node) {
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
    protected string? GetBaseClassName(ClassDeclarationSyntax klass) {
        if (klass.BaseList == null || !klass.BaseList.Types.Any())
            return null;

        var baseTypesIdentifierTexts = klass.BaseList.Types.Select(p => p.Type).OfType<SimpleNameSyntax>().Select(p => p.Identifier.Text);
        return baseTypesIdentifierTexts.FirstOrDefault(it => !it.StartsWith("I", StringComparison.OrdinalIgnoreCase));
    }
}