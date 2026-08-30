using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using ProtocolModels = EmmyLua.LanguageServer.Framework.Protocol.Model;

namespace DotRush.Roslyn.Server.Extensions;

public static class PositionExtensions {
    public static int ToOffset(this ProtocolModels.Position position, SourceText sourceText) {
        if (sourceText.Lines.Count < position.Line)
            return 0;

        return sourceText.Lines.GetPosition(new LinePosition(position.Line, position.Character));
    }

    public static ProtocolModels.Position ToPosition(this int offset, SourceText sourceText) {
        var linePosition = sourceText.Lines.GetLinePosition(offset);
        return new ProtocolModels.Position(linePosition.Line, linePosition.Character);
    }

    public static ProtocolModels.DocumentRange ToRange(this LinePositionSpan span) {
        return new ProtocolModels.DocumentRange(
            new ProtocolModels.Position(span.Start.Line, span.Start.Character),
            new ProtocolModels.Position(span.End.Line, span.End.Character)
        );
    }
    public static ProtocolModels.DocumentRange ToRange(this TextSpan span, SourceText sourceText) {
        return new ProtocolModels.DocumentRange(
            span.Start.ToPosition(sourceText),
            span.End.ToPosition(sourceText)
        );
    }
    public static ProtocolModels.DocumentRange ToRange(this Location location) {
        return location.GetLineSpan().Span.ToRange();
    }
    public static ProtocolModels.DocumentRange ToRange(this ProtocolModels.Position position) {
        return new ProtocolModels.DocumentRange(position, position);
    }

    public static ProtocolModels.Location ToLocation(this Location location) {
        ArgumentNullException.ThrowIfNull(location.SourceTree);
        return new ProtocolModels.Location() {
            Uri = location.SourceTree.FilePath,
            Range = location.SourceSpan.ToRange(location.SourceTree.GetText())
        };
    }
    public static ProtocolModels.Location ToLocation(this FileLinePositionSpan span) {
        return new ProtocolModels.Location() {
            Uri = span.Path,
            Range = span.Span.ToRange()
        };
    }

    public static TextSpan ToTextSpan(this ProtocolModels.DocumentRange range, SourceText sourceText) {
        return TextSpan.FromBounds(
            range.Start.ToOffset(sourceText),
            range.End.ToOffset(sourceText)
        );
    }

    public static bool CheckCollision(ProtocolModels.DocumentRange range1, ProtocolModels.DocumentRange range2) {
        static int Compare(ProtocolModels.Position position1, ProtocolModels.Position position2) {
            int lineCompare = position1.Line.CompareTo(position2.Line);
            if (lineCompare != 0)
                return lineCompare;

            return position1.Character.CompareTo(position2.Character);
        }

        return Compare(range1.Start, range2.End) <= 0 && Compare(range2.Start, range1.End) <= 0;
    }
    public static ProtocolModels.DocumentRange Offset(this ProtocolModels.DocumentRange range, int lineOffset) {
        return new ProtocolModels.DocumentRange {
            Start = new ProtocolModels.Position(range.Start.Line + lineOffset, range.Start.Character),
            End = new ProtocolModels.Position(range.End.Line + lineOffset, range.End.Character)
        };
    }
    public static int Height(this ProtocolModels.DocumentRange range) {
        return range.End.Line - range.Start.Line + 1;
    }
}
