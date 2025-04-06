using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Text;
using ProtocolModels = EmmyLua.LanguageServer.Framework.Protocol.Model;

namespace DotRush.Roslyn.Server.Extensions;

public static class PositionExtensions {
    public static int ToOffset(this ProtocolModels.Position position, SourceText sourceText) {
        if (sourceText.Lines.Count < position.Line)
            return 0;

        return sourceText.Lines.GetPosition(new LinePosition(position.Line, position.Character));
    }
    public static ProtocolModels.DocumentRange ToRange(this ProtocolModels.Position position) {
        return new ProtocolModels.DocumentRange(position, position);
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

    public static ProtocolModels.Location? ToLocation(this Location location, string? filePath = null) {
        if (location.SourceTree == null)
            return null;

        return new ProtocolModels.Location() {
            Uri = filePath ?? location.SourceTree.FilePath,
            Range = location.SourceSpan.ToRange(location.SourceTree.GetText())
        };
    }

    public static ProtocolModels.Location? ToLocation(this ReferenceLocation location, SourceText sourceText) {
        if (location.Document.FilePath == null)
            return null;

        return new ProtocolModels.Location() {
            Uri = location.Document.FilePath,
            Range = location.Location.SourceSpan.ToRange(sourceText)
        };
    }

    public static TextSpan ToTextSpan(this ProtocolModels.DocumentRange range, SourceText sourceText) {
        return TextSpan.FromBounds(
            range.Start.ToOffset(sourceText),
            range.End.ToOffset(sourceText)
        );
    }

    public static ProtocolModels.Location? ToDecompiledUnknownLocation(string? filePath) {
        if (filePath == null)
            return null;

        return new ProtocolModels.Location() {
            Uri = filePath,
            Range = EmptyRange
        };
    }

    public static ProtocolModels.DocumentRange EmptyRange => new ProtocolModels.DocumentRange(EmptyPosition, EmptyPosition);
    public static ProtocolModels.Position EmptyPosition => new ProtocolModels.Position(0, 0);

    public static bool OverlapsWith(this ProtocolModels.DocumentRange baseRange, ProtocolModels.DocumentRange otherRange) {
        return baseRange.Start.Line <= otherRange.End.Line
            && baseRange.End.Line >= otherRange.Start.Line
            && baseRange.Start.Character <= otherRange.End.Character
            && baseRange.End.Character >= otherRange.Start.Character;
    }
}