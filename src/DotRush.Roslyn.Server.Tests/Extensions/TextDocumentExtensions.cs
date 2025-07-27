using EmmyLua.LanguageServer.Framework.Protocol.Model;
using EmmyLua.LanguageServer.Framework.Protocol.Model.TextDocument;

namespace DotRush.Roslyn.Server.Tests.Extensions;

public static class TextDocumentExtensions {
    public static TextDocumentIdentifier CreateDocument(this string filePath) {
        return new TextDocumentIdentifier(new DocumentUri(new Uri(filePath)));
    }
}