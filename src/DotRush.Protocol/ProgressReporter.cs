using DotRush.Protocol.Models;

namespace DotRush.Protocol;

public class ProgressReporter {
    private readonly LanguageClient client;
    private readonly string token;

    internal ProgressReporter(LanguageClient client, string token) {
        this.client = client;
        this.token = token;
    }

    public void Report(string message, int percentage = 0) {
        client.SendNotification("$/progress", new ProgressParams {
            Value = new WorkDoneProgressReport { Message = message, Percentage = percentage },
            Token = token,
        });
    }
    public void End() {
        client.SendNotification("$/progress", new ProgressParams {
            Value = new WorkDoneProgressEnd(),
            Token = token,
        });
    }
}
