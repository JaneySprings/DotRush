using ProtocolModels = EmmyLua.LanguageServer.Framework.Protocol.Model;

namespace DotRush.Roslyn.Server.Extensions;

public static class PositionExtensions {
    public static ProtocolModels.DocumentRange EmptyRange => new ProtocolModels.DocumentRange(new ProtocolModels.Position(0, 0), new ProtocolModels.Position(0, 0));
}