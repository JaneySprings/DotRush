// https://github.com/dotnet/roslyn/issues/56859

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DotRush.Roslyn.CodeAnalysis.Embedded.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class UseIsNullCheckDiagnosticAnalyzer : DiagnosticAnalyzer {
    public const string DiagnosticId = "DR0002";

    private static readonly DiagnosticDescriptor descriptor = new DiagnosticDescriptor(
        DiagnosticId,
        "Use pattern matching for null check",
        "Null check can be replaced with pattern matching",
        "Style",
        DiagnosticSeverity.Hidden,
        isEnabledByDefault: true
    );

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeNode, SyntaxKind.EqualsExpression, SyntaxKind.NotEqualsExpression);
    }

    private static void AnalyzeNode(SyntaxNodeAnalysisContext context) {
        var binaryExpression = (BinaryExpressionSyntax)context.Node;
        var isLeftNull = binaryExpression.Left.IsKind(SyntaxKind.NullLiteralExpression);
        if (isLeftNull == binaryExpression.Right.IsKind(SyntaxKind.NullLiteralExpression))
            return;
        if (binaryExpression.SyntaxTree.Options is not CSharpParseOptions { LanguageVersion: >= LanguageVersion.CSharp9 })
            return;

        // Overloaded operator can have a custom logic (e.g. UnityEngine.Object)
        if (context.SemanticModel.GetSymbolInfo(binaryExpression, context.CancellationToken).Symbol is IMethodSymbol { MethodKind: MethodKind.UserDefinedOperator })
            return;
        if (context.SemanticModel.GetConstantValue(binaryExpression, context.CancellationToken).HasValue)
            return;

        var type = context.SemanticModel.GetTypeInfo(isLeftNull ? binaryExpression.Right : binaryExpression.Left, context.CancellationToken).Type;
        if (type == null || type.TypeKind is TypeKind.Error or TypeKind.Dynamic or TypeKind.Pointer or TypeKind.FunctionPointer)
            return;
        if (type.IsValueType && type.OriginalDefinition.SpecialType != SpecialType.System_Nullable_T)
            return;

        // Pattern matching is not allowed in the expression trees
        var expressionType = context.SemanticModel.Compilation.GetTypeByMetadataName("System.Linq.Expressions.Expression`1");
        foreach (var ancestor in binaryExpression.Ancestors()) {
            if (ancestor is QueryExpressionSyntax)
                return;
            if (ancestor is not AnonymousFunctionExpressionSyntax)
                continue;

            var convertedType = context.SemanticModel.GetTypeInfo(ancestor, context.CancellationToken).ConvertedType;
            if (SymbolEqualityComparer.Default.Equals(convertedType?.OriginalDefinition, expressionType))
                return;
        }

        context.ReportDiagnostic(Diagnostic.Create(descriptor, binaryExpression.GetLocation()));
    }
}