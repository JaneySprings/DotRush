// https://github.com/dotnet/roslyn/blob/0a6e36a0644b544f28de972709b18c962c067888/src/Features/Core/Portable/CodeFixes/Suppression/AbstractSuppressionCodeFixProvider.cs

using System.Collections.Immutable;
using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;

namespace DotRush.Roslyn.CodeAnalysis.Embedded.CodeFixes;

// Wrapper for CSharpSuppressionCodeFixProvider
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(SuppressionCodeFixProvider)), Shared]
public class SuppressionCodeFixProvider : CodeFixProvider {
    // AbstractSuppressionCodeFixProvider.IsFixableDiagnostic(Diagnostic diagnostic)
    public override ImmutableArray<string> FixableDiagnosticIds => throw new NotImplementedException();

    public override Task RegisterCodeFixesAsync(CodeFixContext context) {
        throw new NotImplementedException();
        // AbstractSuppressionCodeFixProvider.GetFixesAsync(...).Select(x => x.CodeAction)
    }
    public sealed override FixAllProvider? GetFixAllProvider() {
        throw new NotImplementedException();
        // AbstractSuppressionCodeFixProvider.GetFixAllProvider()
    }
}