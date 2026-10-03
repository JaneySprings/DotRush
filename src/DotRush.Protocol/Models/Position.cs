namespace DotRush.Protocol.Models;

public readonly record struct Position(int Line, int Character);

public readonly record struct DocumentRange(Position Start, Position End);

public readonly record struct Location(DocumentUri Uri, DocumentRange Range);
