using LanguageServer.Parameters.TextDocument;
using Microsoft.CodeAnalysis;

namespace dotRush.Server.Extensions;

public static class SemanticConverter {
    public static List<DocumentSymbol> ToDocumentSymbols(this SyntaxNode node, SemanticModel model, Document document) {
        var result = new List<DocumentSymbol>();
        
        //Todo

        return result;
    }
}