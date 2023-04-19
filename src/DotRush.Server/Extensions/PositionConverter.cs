using DotRush.Server.Services;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Text;

namespace DotRush.Server.Extensions;

public static class PositionConverter {
    public static int ToOffset(this LanguageServer.Parameters.Position position, Document document) {
        var text = document.GetTextAsync().Result;
        return text.Lines.GetPosition(new LinePosition(
            (int)position.line, 
            (int)position.character 
        ));
    }

    public static LanguageServer.Parameters.Position ToPosition(this int offset, Document document) {
        var text = document.GetTextAsync().Result;
        var linePosition = text.Lines.GetLinePosition(offset);

        var position = new LanguageServer.Parameters.Position();
        position.line = (uint)linePosition.Line;
        position.character = (uint)linePosition.Character;

        return position;
    }

    public static LanguageServer.Parameters.Range ToRange(this LinePositionSpan span) {
        var range = new LanguageServer.Parameters.Range();

        var startPosition = new LanguageServer.Parameters.Position();
        startPosition.line = (uint)span.Start.Line;
        startPosition.character = (uint)span.Start.Character;

        var endPosition = new LanguageServer.Parameters.Position();
        endPosition.line = (uint)span.End.Line;
        endPosition.character = (uint)span.End.Character;

        range.start = startPosition;
        range.end = endPosition;

        return range;
    }

    public static LanguageServer.Parameters.Range ToRange(this TextSpan span, Document document) {
        var range = new LanguageServer.Parameters.Range();

        range.start = span.Start.ToPosition(document);
        range.end = span.End.ToPosition(document);

        return range;
    }

    public static LanguageServer.Parameters.Location? ToLocation(this Location location) {
        var loc = new LanguageServer.Parameters.Location();
        var document = DocumentService.GetDocumentByPath(location.SourceTree?.FilePath);
        if (document == null) 
            return null;

        loc.uri = document.FilePath?.ToUri();
        loc.range = location.SourceSpan.ToRange(document);
        return loc;
    }

    public static LanguageServer.Parameters.Location ToLocation(this ReferenceLocation location) {
        var loc = new LanguageServer.Parameters.Location();
        loc.uri = location.Document.FilePath?.ToUri();
        loc.range = location.Location.SourceSpan.ToRange(location.Document);
        return loc;
    }

    public static TextSpan ToTextSpan(this LanguageServer.Parameters.Range range, Document document) {
        var startPosition = range.start.ToOffset(document);
        var endPosition = range.end.ToOffset(document);
        return TextSpan.FromBounds(startPosition, endPosition);
    }
}