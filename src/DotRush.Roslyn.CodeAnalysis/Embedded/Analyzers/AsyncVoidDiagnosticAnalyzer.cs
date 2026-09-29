// https://github.com/dotnet/roslyn-analyzers/issues/1576

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DotRush.Roslyn.CodeAnalysis.Embedded.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class AsyncVoidDiagnosticAnalyzer : DiagnosticAnalyzer {
    public const string DiagnosticId = "DR0003";

    private static readonly DiagnosticDescriptor descriptor = new DiagnosticDescriptor(
        DiagnosticId,
        "Avoid async void methods",
        "Exceptions thrown by the async void method '{0}' cannot be handled by the caller",
        "Reliability",
        DiagnosticSeverity.Info,
        isEnabledByDefault: true
    );

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeNode, SyntaxKind.MethodDeclaration, SyntaxKind.LocalFunctionStatement);
    }

    private static void AnalyzeNode(SyntaxNodeAnalysisContext context) {
        var (modifiers, returnType, identifier) = context.Node switch {
            MethodDeclarationSyntax method => (method.Modifiers, method.ReturnType, method.Identifier),
            LocalFunctionStatementSyntax localFunction => (localFunction.Modifiers, localFunction.ReturnType, localFunction.Identifier),
            _ => default
        };
        if (!modifiers.Any(SyntaxKind.AsyncKeyword) || returnType is not PredefinedTypeSyntax predefinedType || !predefinedType.Keyword.IsKind(SyntaxKind.VoidKeyword))
            return;
        if (context.SemanticModel.GetDeclaredSymbol(context.Node, context.CancellationToken) is not IMethodSymbol methodSymbol)
            return;

        // Signature cannot be changed
        if (methodSymbol.IsOverride || methodSymbol.ExplicitInterfaceImplementations.Length > 0)
            return;
        if (methodSymbol.Parameters.Length == 2 && methodSymbol.Parameters[1].Type.Name.EndsWith("EventArgs", StringComparison.Ordinal))
            return;
        if (methodSymbol.MethodKind == MethodKind.Ordinary) {
            foreach (var interfaceMember in methodSymbol.ContainingType.AllInterfaces.SelectMany(it => it.GetMembers(methodSymbol.Name))) {
                if (SymbolEqualityComparer.Default.Equals(methodSymbol.ContainingType.FindImplementationForInterfaceMember(interfaceMember), methodSymbol))
                    return;
            }
        }

        context.ReportDiagnostic(Diagnostic.Create(descriptor, identifier.GetLocation(), methodSymbol.Name));
    }
}