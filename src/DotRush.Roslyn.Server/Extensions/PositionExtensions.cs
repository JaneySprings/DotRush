using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Text;
using OmniSharp.Extensions.LanguageServer.Protocol;
using ProtocolModels = OmniSharp.Extensions.LanguageServer.Protocol.Models;

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

    public static ProtocolModels.Range ToRange(this LinePositionSpan span) {
        return new ProtocolModels.Range(
            new ProtocolModels.Position(span.Start.Line, span.Start.Character),
            new ProtocolModels.Position(span.End.Line, span.End.Character)
        );
    }

    public static ProtocolModels.Range ToRange(this TextSpan span, SourceText sourceText) {
        return new ProtocolModels.Range(
            span.Start.ToPosition(sourceText),
            span.End.ToPosition(sourceText)
        );
    }

    public static ProtocolModels.Range ToRange(this Location location) {
        return location.GetLineSpan().Span.ToRange();
    }

    public static ProtocolModels.Location? ToLocation(this Location location) {
        if (location.SourceTree == null)
            return null;

        return new ProtocolModels.Location() {
            Uri = DocumentUri.FromFileSystemPath(location.SourceTree.FilePath),
            Range = location.SourceSpan.ToRange(location.SourceTree.GetText())
        };
    }

    public static ProtocolModels.Location? ToLocation(this ReferenceLocation location, SourceText sourceText) {
        if (location.Document.FilePath == null)
            return null;

        return new ProtocolModels.Location() {
            Uri = DocumentUri.FromFileSystemPath(location.Document.FilePath),
            Range = location.Location.SourceSpan.ToRange(sourceText)
        };
    }

    public static TextSpan ToTextSpan(this ProtocolModels.Range range, SourceText sourceText) {
        return TextSpan.FromBounds(
            range.Start.ToOffset(sourceText),
            range.End.ToOffset(sourceText)
        );
    }

    public static ProtocolModels.Range EmptyRange => new ProtocolModels.Range(new ProtocolModels.Position(0, 0), new ProtocolModels.Position(0, 0));
}