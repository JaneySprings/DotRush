using LanguageServer.Parameters;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace dotRush.Server.Extensions;

public static class PositionConverter {
    public static int ToOffset(this Position position, Document document) {
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

        var startPosition = new Position();
        startPosition.line = (uint)span.Start.Line;
        startPosition.character = (uint)span.Start.Character;

        var endPosition = new Position();
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
}