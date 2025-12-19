using System.Collections.ObjectModel;
using DotRush.Roslyn.CodeAnalysis.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DotRush.Roslyn.Server.Services;

public class TestExplorerService {
    private readonly string[] knownTestCaseAttributes = new[] {
        "Xunit.FactAttribute",
        "Xunit.TheoryAttribute",
        "NUnit.Framework.TestAttribute",
        "NUnit.Framework.TestCaseAttribute",
        "NUnit.Framework.TestCaseSourceAttribute",
        "Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute",
        "TUnit.Core.TestAttribute",
        "GdUnit4.TestSuiteAttribute",
        "GdUnit4.TestCaseAttribute"
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
    public async Task<ReadOnlyCollection<IMethodSymbol>> GetTestCasesAsync(Document document, string fixtureFullName, CancellationToken cancellationToken) {
        var fixtureSymbols = await GetTestFixturesAsync(document, cancellationToken).ConfigureAwait(false);
        var fixtureSymbol = fixtureSymbols.FirstOrDefault(s => s.GetFullName() == fixtureFullName);
        ArgumentNullException.ThrowIfNull(fixtureSymbol, nameof(fixtureSymbol));

        return GetTestCasesCore(fixtureSymbol).AsReadOnly();
    }

    private async Task<INamedTypeSymbol[]> GetFixturesCoreAsync(SemanticModel semanticModel, CancellationToken cancellationToken) {
        var root = await semanticModel.SyntaxTree.GetRootAsync(cancellationToken).ConfigureAwait(false);
        return root.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .Select(c => semanticModel.GetDeclaredSymbol(c))
            .OfType<INamedTypeSymbol>()
            .Where(s => !s.IsAbstract && !s.IsStatic)
            .Where(symbol => MemberHasAttribute(symbol, knownTestCaseAttributes))
            .ToArray();
    }
    private IMethodSymbol[] GetTestCasesCore(INamedTypeSymbol fixtureSymbol) {
        return fixtureSymbol.GetAllMembers()
            .OfType<IMethodSymbol>()
            .Where(m => m.GetAttributes().Any(a => knownTestCaseAttributes.Contains(a.AttributeClass?.GetFullName())))
            .ToArray();
    }

    private static bool MemberHasAttribute(INamedTypeSymbol symbol, params string[] attributeNames) {
        bool CheckAttribute(INamedTypeSymbol symbol, params string[] names) {
            return symbol.GetMembers()
                .SelectMany(m => m.GetAttributes())
                .Any(a => names.Contains(a.AttributeClass?.GetFullName() ?? string.Empty));
        }

        if (CheckAttribute(symbol, attributeNames))
            return true;

        while (symbol.BaseType != null) {
            symbol = symbol.BaseType;
            if (CheckAttribute(symbol, attributeNames))
                return true;
        }

        return false;
    }
}