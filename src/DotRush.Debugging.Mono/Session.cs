using DotRush.Common.Interop;
using DotRush.Common.Logging;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Mono.Debugging.Client;

namespace DotRush.Debugging.Mono;

public abstract class Session : DebugAdapterBase, IProcessLogger {

    protected Session(Stream input, Stream output) {
        LogConfig.InitializeLog();
        InitializeProtocolClient(input, output);
    }

    protected abstract void OnUnhandledException(Exception ex);

    public void Start() {
        Protocol.LogMessage += LogMessage;
        Protocol.DispatcherError += LogError;
        Protocol.Run();
    }
    public void OnOutputDataReceived(string stdout) {
        SendMessageEvent(OutputEvent.CategoryValue.Stdout, stdout);
    }
    public void OnErrorDataReceived(string stderr) {
        SendMessageEvent(OutputEvent.CategoryValue.Stderr, stderr);
    }
    public void OnDebugDataReceived(string debug) {
        SendMessageEvent(OutputEvent.CategoryValue.Console, debug);
    }
    public void OnImportantDataReceived(string message) {
        SendMessageEvent(OutputEvent.CategoryValue.Important, message);
    }

    private void SendMessageEvent(OutputEvent.CategoryValue category, string message) {
        Protocol.SendEvent(new OutputEvent(message.Trim() + Environment.NewLine) {
            Category = category
        });
    }
    private void LogMessage(object? sender, LogEventArgs args) {
        DebuggerLoggingService.CustomLogger?.LogMessage(args.Message);
    }
    private void LogError(object? sender, DispatcherErrorEventArgs args) {
        DebuggerLoggingService.CustomLogger?.LogError($"[Fatal] {args.Exception.Message}", args.Exception);
        OnUnhandledException(args.Exception);
    }
}