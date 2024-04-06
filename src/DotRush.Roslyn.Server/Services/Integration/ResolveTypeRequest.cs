using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Text;

namespace DotRush.Roslyn.Server.Services;

//https://github.com/mono/monodevelop/blob/ba01d2d6d3c84e92a6b5f360dac76ee821547529/main/src/addins/MonoDevelop.Debugger/MonoDevelop.Debugger/DebuggingService.cs#L1472
internal class ResolveTypeRequest {
    private readonly WorkspaceService workspaceService;
    private readonly SymbolDisplayFormat displayFormat = new SymbolDisplayFormat(
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers | SymbolDisplayMiscellaneousOptions.UseSpecialTypes
    );

    public ResolveTypeRequest(WorkspaceService workspaceService) {
        this.workspaceService = workspaceService;
    }
    
    public async Task<string?> HandleAsync(string[] parameters) {
        if (parameters.Length < 3)
            return null;

        var documentPath = parameters[0];
        var position = new LinePosition(int.Parse(parameters[1])-1, 0);
        var typeName = parameters[2];

        var documentId = workspaceService.Solution?.GetDocumentIdsWithFilePath(documentPath).FirstOrDefault();
        var document = workspaceService.Solution?.GetDocument(documentId);
        if (document == null)
            return null;

        var sourceText = await document.GetTextAsync();
        var offset = sourceText.Lines.GetPosition(position);
        var textChange = new TextChange(new TextSpan(offset, 0), $"{typeName};");
        document = document.WithText(sourceText.WithChanges(textChange));

        var symbol = await SymbolFinder.FindSymbolAtPositionAsync(document, offset+1, CancellationToken.None);
        if (symbol is INamespaceSymbol namespaceSymbol)
            return namespaceSymbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);
        if (symbol is INamedTypeSymbol namedTypeSymbol && namedTypeSymbol.TypeKind != TypeKind.Dynamic)
            return namedTypeSymbol.ToDisplayString(displayFormat);

        return typeName;
    }
}