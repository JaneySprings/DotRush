using DotRush.Server.Services;
using LanguageServer.Parameters;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;

namespace DotRush.Server.Extensions;

public static class SemanticConverter {
    public static ISymbol? GetSymbolForPosition(Position position, string? path) {
        var document = DocumentService.GetDocumentByPath(path);
        if (document == null) 
            return null;
        var offset = position.ToOffset(document);
        return SymbolFinder.FindSymbolAtPositionAsync(document, offset).Result;
    }
}