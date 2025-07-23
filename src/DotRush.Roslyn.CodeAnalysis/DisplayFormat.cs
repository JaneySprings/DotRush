using Microsoft.CodeAnalysis;

namespace DotRush.Roslyn.CodeAnalysis;

public static class DisplayFormat {
    public static readonly SymbolDisplayFormat Default = new SymbolDisplayFormat(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters | SymbolDisplayGenericsOptions.IncludeVariance,
        memberOptions: SymbolDisplayMemberOptions.IncludeParameters | SymbolDisplayMemberOptions.IncludeType | SymbolDisplayMemberOptions.IncludeRef | SymbolDisplayMemberOptions.IncludeContainingType | SymbolDisplayMemberOptions.IncludeConstantValue,
        parameterOptions: SymbolDisplayParameterOptions.IncludeExtensionThis | SymbolDisplayParameterOptions.IncludeParamsRefOut | SymbolDisplayParameterOptions.IncludeType,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers,
        delegateStyle: SymbolDisplayDelegateStyle.NameAndSignature
    );

    public static readonly SymbolDisplayFormat Minimal = SymbolDisplayFormat.MinimallyQualifiedFormat
        .WithMemberOptions(SymbolDisplayMemberOptions.IncludeParameters
            | SymbolDisplayMemberOptions.IncludeType
            | SymbolDisplayMemberOptions.IncludeRef
            | SymbolDisplayMemberOptions.IncludeContainingType
            | SymbolDisplayMemberOptions.IncludeConstantValue
        );

    public static readonly SymbolDisplayFormat Member = new SymbolDisplayFormat(
        genericsOptions:
            SymbolDisplayGenericsOptions.IncludeTypeParameters |
            SymbolDisplayGenericsOptions.IncludeVariance,
        memberOptions:
            SymbolDisplayMemberOptions.IncludeParameters,
        parameterOptions:
            SymbolDisplayParameterOptions.IncludeExtensionThis |
            SymbolDisplayParameterOptions.IncludeParamsRefOut |
            SymbolDisplayParameterOptions.IncludeType,
        miscellaneousOptions:
            SymbolDisplayMiscellaneousOptions.UseSpecialTypes
    );
}