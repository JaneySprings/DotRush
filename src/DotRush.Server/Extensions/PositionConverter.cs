using DotRush.Server.Services;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Text;
using OmniSharp.Extensions.LanguageServer.Protocol;
using ProtocolModels = OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace DotRush.Server.Extensions;

public static class PositionConverter {
    public static int ToOffset(this ProtocolModels.Position position, Document document) {
        var text = document.GetTextAsync().Result;
        return text.Lines.GetPosition(new LinePosition(
            position.Line, 
            position.Character 
        ));
    }

    public static ProtocolModels.Position ToPosition(this int offset, Document document) {
        var text = document.GetTextAsync().Result;
        var linePosition = text.Lines.GetLinePosition(offset);

        return new ProtocolModels.Position(linePosition.Line, linePosition.Character);
    }

    public static ProtocolModels.Range ToRange(this LinePositionSpan span) {
        return new ProtocolModels.Range(
            new ProtocolModels.Position(span.Start.Line, span.Start.Character),
            new ProtocolModels.Position(span.End.Line, span.End.Character)
        );
    }

    public static ProtocolModels.Range ToRange(this TextSpan span, Document document) {
        return new ProtocolModels.Range(
            span.Start.ToPosition(document),
            span.End.ToPosition(document)
        );
    }

    public static ProtocolModels.Location? ToLocation(this Location location, SolutionService service) {
        var document = service.GetDocumentByPath(location.SourceTree!.FilePath);
        if (document == null) 
            return null;

        return new ProtocolModels.Location() {
            Uri = DocumentUri.From(document.FilePath!),
            Range = location.SourceSpan.ToRange(document)
        };
    }

    public static ProtocolModels.Location ToLocation(this ReferenceLocation location) {
        return new ProtocolModels.Location() {
            Uri = DocumentUri.From(location.Document.FilePath!),
            Range = location.Location.SourceSpan.ToRange(location.Document)
        };
    }

    public static TextSpan ToTextSpan(this ProtocolModels.Range range, Document document) {
        return TextSpan.FromBounds(
            range.Start.ToOffset(document), 
            range.End.ToOffset(document)
        );
    }
}