using EmmyLua.LanguageServer.Framework.Protocol.Model;
using EmmyLua.LanguageServer.Framework.Protocol.Model.TextDocument;
using Microsoft.CodeAnalysis;

namespace DotRush.Roslyn.Server.Tests.Extensions;

public static class TextDocumentExtensions {
    public static TextDocumentIdentifier CreateDocumentId(this string filePath) {
        return new TextDocumentIdentifier(new DocumentUri(new Uri(filePath)));
    }
    public static TextDocumentIdentifier CreateDocumentId(this Document document) {
        return new TextDocumentIdentifier(new DocumentUri(new Uri(document.FilePath!)));
    }
    public static string ToLF(this string text) {
        return text.Replace("\r\n", "\n");
    }
}