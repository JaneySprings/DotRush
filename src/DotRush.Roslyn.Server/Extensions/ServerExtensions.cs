using System.Collections.ObjectModel;
using System.Text.Json;
using DotRush.Common.Logging;
using EmmyLua.LanguageServer.Framework.Protocol.JsonRpc;
using EmmyLua.LanguageServer.Framework.Protocol.Message.Client.ShowMessage;
using EmmyLua.LanguageServer.Framework.Protocol.Message.Configuration;
using EmmyLua.LanguageServer.Framework.Protocol.Message.Progress;
using EmmyLua.LanguageServer.Framework.Protocol.Model;
using EmmyLua.LanguageServer.Framework.Protocol.Model.WorkDoneProgress;
using EmmyLua.LanguageServer.Framework.Server;
using EmmyLuaLanguageServer = EmmyLua.LanguageServer.Framework.Server.LanguageServer;

namespace DotRush.Roslyn.Server.Extensions;

public static class ServerExtensions {
    public static void ShowError(this ClientProxy clientProxy, string message) {
        _ = clientProxy.ShowErrorAsync(message);
    }
    public static void ShowInfo(this ClientProxy clientProxy, string message) {
        _ = clientProxy.ShowInfoAsync(message);
    }
    public static Task ShowErrorAsync(this ClientProxy clientProxy, string message) {
        CurrentSessionLogger.Error(message);
        return clientProxy.ShowMessage(new ShowMessageParams {
            Type = MessageType.Error,
            Message = message
        });
    }
    public static Task ShowInfoAsync(this ClientProxy clientProxy, string message) {
        CurrentSessionLogger.Debug(message);
        return clientProxy.ShowMessage(new ShowMessageParams {
            Type = MessageType.Info,
            Message = message
        });
    }

    public static async Task CreateWorkDoneProgress(this EmmyLuaLanguageServer server, string token) {
        await server.SendRequest("window/workDoneProgress/create", JsonSerializer.SerializeToDocument(new ProgressParams {
            Token = Resources.WorkspaceServiceWorkDoneToken,
        }), CancellationToken.None).ConfigureAwait(false);

        await server.SendNotification("$/progress", JsonSerializer.SerializeToDocument(new ProgressParams {
            Token = Resources.WorkspaceServiceWorkDoneToken,
            Value = new WorkDoneProgressBegin() { Percentage = 0 },
        })).ConfigureAwait(false);
    }
    public static Task UpdateWorkDoneProgress(this EmmyLuaLanguageServer server, string token, string message) {
        return server.SendNotification("$/progress", JsonSerializer.SerializeToDocument(new ProgressParams {
            Value = new WorkDoneProgressReport() { Message = message, Percentage = 0 },
            Token = token,
        }));
    }
    public static Task EndWorkDoneProgress(this EmmyLuaLanguageServer server, string token) {
        return server.SendNotification("$/progress", JsonSerializer.SerializeToDocument(new ProgressParams {
            Value = new WorkDoneProgressEnd(),
            Token = token,
        }));
    }
    public static Task SendNotification(this EmmyLuaLanguageServer server, string method, JsonDocument? parameters) {
        return server.SendNotification(new NotificationMessage(method, parameters));
    }

    public static ReadOnlyDictionary<string, string> ToPropertiesDictionary(this List<string> properties) {
        var result = new Dictionary<string, string>();
        foreach (var property in properties) {
            var keyValue = property.Split('=');
            if (keyValue.Length != 2)
                continue;
            result[keyValue[0]] = keyValue[1];
        }

        return new ReadOnlyDictionary<string, string>(result);
    }
}
