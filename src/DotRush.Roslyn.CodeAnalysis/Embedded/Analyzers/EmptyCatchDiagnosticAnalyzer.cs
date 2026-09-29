// https://github.com/dotnet/roslyn/issues/84408

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DotRush.Roslyn.CodeAnalysis.Embedded.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class EmptyCatchDiagnosticAnalyzer : DiagnosticAnalyzer {
    public const string DiagnosticId = "DR0004";

    private static readonly DiagnosticDescriptor descriptor = new DiagnosticDescriptor(
        DiagnosticId,
        "Avoid empty catch blocks",
        "Empty catch block swallows all exceptions",
        "Reliability",
        DiagnosticSeverity.Info,
        isEnabledByDefault: true
    );

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeNode, SyntaxKind.CatchClause);
    }

    private static void AnalyzeNode(SyntaxNodeAnalysisContext context) {
        var catchClause = (CatchClauseSyntax)context.Node;
        if (catchClause.Filter != null || catchClause.Block.Statements.Count != 0)
            return;

        // Comment explains why the exception is ignored
        var trivia = catchClause.Block.OpenBraceToken.TrailingTrivia.Concat(catchClause.Block.CloseBraceToken.LeadingTrivia);
        if (trivia.Any(it => !it.IsKind(SyntaxKind.WhitespaceTrivia) && !it.IsKind(SyntaxKind.EndOfLineTrivia)))
            return;

        if (catchClause.Declaration != null) {
            var exceptionType = context.SemanticModel.Compilation.GetTypeByMetadataName("System.Exception");
            var type = context.SemanticModel.GetTypeInfo(catchClause.Declaration.Type, context.CancellationToken).Type;
            if (!SymbolEqualityComparer.Default.Equals(type, exceptionType))
                return;
        }

        context.ReportDiagnostic(Diagnostic.Create(descriptor, catchClause.CatchKeyword.GetLocation()));
    }
}