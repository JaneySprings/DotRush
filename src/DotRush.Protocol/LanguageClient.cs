using DotRush.Protocol.Models;

namespace DotRush.Protocol;

public class LanguageClient {
    private readonly LanguageServer server;

    internal LanguageClient(LanguageServer server) {
        this.server = server;
    }

    public void SendNotification(string method, object? parameters = null) {
        server.SendNotification(method, parameters);
    }
    public Task<TResult?> SendRequestAsync<TResult>(string method, object? parameters, CancellationToken cancellationToken) {
        return server.SendRequestAsync<TResult>(method, parameters, cancellationToken);
    }
    public Task SendRequestAsync(string method, object? parameters, CancellationToken cancellationToken) {
        return server.SendRequestAsync<object>(method, parameters, cancellationToken);
    }

    public void ShowMessage(MessageType type, string message) {
        SendNotification("window/showMessage", new ShowMessageParams { Type = type, Message = message });
    }
    public void PublishDiagnostics(PublishDiagnosticsParams parameters) {
        SendNotification("textDocument/publishDiagnostics", parameters);
    }
    public Task RefreshSemanticTokensAsync(CancellationToken cancellationToken) {
        return SendRequestAsync("workspace/semanticTokens/refresh", null, cancellationToken);
    }
    public async Task<ProgressReporter> CreateProgressAsync(string title, CancellationToken cancellationToken) {
        var token = Guid.NewGuid().ToString();
        await SendRequestAsync("window/workDoneProgress/create", new WorkDoneProgressCreateParams { Token = token }, cancellationToken).ConfigureAwait(false);
        SendNotification("$/progress", new ProgressParams {
            Value = new WorkDoneProgressBegin { Title = title },
            Token = token,
        });
        return new ProgressReporter(this, token);
    }
}
