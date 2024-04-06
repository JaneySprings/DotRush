using DotRush.Roslyn.Common.Logging;
using Microsoft.Extensions.Configuration;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Extensions.LanguageServer.Protocol.Window;

namespace DotRush.Roslyn.Server.Extensions;

public static class ServerExtensions {
    public static void ShowError(this ILanguageServerFacade server, string message) {
        CurrentSessionLogger.Error(message);
        server.Window.ShowMessage(new ShowMessageParams {
            Type = MessageType.Error,
            Message = message
        });
    }
    public static void ShowInfo(this ILanguageServerFacade server, string message) {
        CurrentSessionLogger.Debug(message);
        server.Window.ShowMessage(new ShowMessageParams {
            Type = MessageType.Info,
            Message = message
        });
    }

    public static Dictionary<string, string> GetKeyValuePairs(this ILanguageServerConfiguration configuration, string key) {
        var result = new Dictionary<string, string>();
        for (byte i = 0; i < byte.MaxValue; i++) {
            var option = configuration?.GetValue<string>($"{key}:{i}");
            if (option == null)
                break;

            var keyValue = option.Split('=');
            if (keyValue.Length != 2)
                continue;
            result[keyValue[0]] = keyValue[1];
        }

        return result;
    }
}