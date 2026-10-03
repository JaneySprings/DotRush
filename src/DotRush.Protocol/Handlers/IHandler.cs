using DotRush.Protocol.Models;

namespace DotRush.Protocol.Handlers;

public interface IHandler {
    void RegisterHandler(LanguageServer server);
    void RegisterCapability(ServerCapabilities serverCapabilities) { }
}
