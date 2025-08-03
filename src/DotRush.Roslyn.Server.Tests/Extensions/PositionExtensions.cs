using EmmyLua.LanguageServer.Framework.Protocol.Model;

namespace DotRush.Roslyn.Server.Tests.Extensions;

public static class PositionExtensions {
    public static Position CreatePosition(int line, int character) {
        return new Position(line, character);
    }
    public static Position CreatePosition(int line) {
        return CreatePosition(line, 0);
    }

    public static DocumentRange CreateRange(int startLine, int startCharacter, int endLine, int endCharacter) {
        return new DocumentRange(
            CreatePosition(startLine, startCharacter),
            CreatePosition(endLine, endCharacter)
        );
    }
    public static DocumentRange CreateRange(int startLine, int endLine) {
        return new DocumentRange(
            CreatePosition(startLine),
            CreatePosition(endLine)
        );
    }

}