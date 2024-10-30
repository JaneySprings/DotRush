using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Text;

namespace DotRush.Roslyn.ExternalAccess.Handlers;

public class SourceLocation {
    public string? FileName { get; set; }
    public int Line { get; set; }
    public int Column { get; set; }
    public int EndLine { get; set; }
    public int EndColumn { get; set; }
}

public interface IExternalTypeResolver {
    string? HandleResolveType(string identifierName, SourceLocation location);
}

public static class ExternalTypeResolver {
    private static readonly SymbolDisplayFormat displayFormat = new SymbolDisplayFormat(
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers | SymbolDisplayMiscellaneousOptions.UseSpecialTypes
    );
    // This method was copied from MonoDevelop
    public static string? Handle(string identifierName, SourceLocation location, Solution? solution) {
        var documentId = solution?.GetDocumentIdsWithFilePath(location.FileName).FirstOrDefault();
        var document = solution?.GetDocument(documentId);
        if (document == null)
            return null;

        var sourceText = document.GetTextAsync().Result;
        var offset = sourceText.Lines.GetPosition(new LinePosition(location.Line-1, 0));
        var textChange = new TextChange(new TextSpan(offset, 0), $"{identifierName};");
        document = document.WithText(sourceText.WithChanges(textChange));

        var symbol = SymbolFinder.FindSymbolAtPositionAsync(document, offset+1, CancellationToken.None).Result;
        if (symbol is INamespaceSymbol namespaceSymbol)
            return namespaceSymbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);
        if (symbol is INamedTypeSymbol namedTypeSymbol && namedTypeSymbol.TypeKind != TypeKind.Dynamic)
            return namedTypeSymbol.ToDisplayString(displayFormat);

        return identifierName;
    }
}
