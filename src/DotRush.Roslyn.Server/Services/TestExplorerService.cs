using System.Collections.ObjectModel;
using DotRush.Roslyn.Server.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DotRush.Roslyn.Server.Services;

public class TestExplorerService {
    private readonly string[] knownTestCaseAttributes = new[] {
        "Xunit.FactAttribute",
        "Xunit.TheoryAttribute",
        "NUnit.Framework.TestAttribute",
        "NUnit.Framework.TestCaseAttribute",
        "Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute"
    };

    public TestExplorerService() {
    }

    public async Task<ReadOnlyCollection<INamedTypeSymbol>> GetTestFixturesAsync(Project project, CancellationToken cancellationToken) {
        var compilation = await project.GetCompilationAsync(cancellationToken).ConfigureAwait(false);
        ArgumentNullException.ThrowIfNull(compilation, nameof(compilation));

        var fixtureSymbols = new List<INamedTypeSymbol>();
        foreach (var syntaxTree in compilation.SyntaxTrees) {
            var model = compilation.GetSemanticModel(syntaxTree);
            var symbols = await GetFixturesCoreAsync(model, cancellationToken).ConfigureAwait(false);
            fixtureSymbols.AddRange(symbols);
        }

        return fixtureSymbols.AsReadOnly();
    }
    public async Task<ReadOnlyCollection<INamedTypeSymbol>> GetTestFixturesAsync(Document document, CancellationToken cancellationToken) {
        var model = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
        ArgumentNullException.ThrowIfNull(model, nameof(model));

        var symbols = await GetFixturesCoreAsync(model, cancellationToken).ConfigureAwait(false);
        return symbols.AsReadOnly();
    }

    private async Task<INamedTypeSymbol[]> GetFixturesCoreAsync(SemanticModel semanticModel, CancellationToken cancellationToken) {
        var root = await semanticModel.SyntaxTree.GetRootAsync(cancellationToken).ConfigureAwait(false);
        return root.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .Select(c => semanticModel.GetDeclaredSymbol(c))
            .OfType<INamedTypeSymbol>()
            .Where(s => !s.IsAbstract && !s.IsStatic)
            .Where(symbol => symbol.GetMembers().Any(m => m.HasAttribute(knownTestCaseAttributes)))
            .ToArray();
    }
}