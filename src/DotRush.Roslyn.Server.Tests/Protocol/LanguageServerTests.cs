using System.IO.Pipelines;
using System.Text;
using System.Text.Json;
using DotRush.Protocol;
using DotRush.Protocol.Models;
using NUnit.Framework;

namespace DotRush.Roslyn.Server.Tests;

public class LanguageServerTests {
    private static readonly TimeSpan timeout = TimeSpan.FromSeconds(10);
    private LanguageServer server;
    private Stream clientWriter;
    private Stream clientReader;
    private Task<int>? serverTask;

    [SetUp]
    public void SetUp() {
        var input = new Pipe();
        var output = new Pipe();
        clientWriter = input.Writer.AsStream();
        clientReader = output.Reader.AsStream();
        server = new LanguageServer(input.Reader.AsStream(), output.Writer.AsStream());
        serverTask = null;
    }
    [TearDown]
    public async Task TearDown() {
        clientWriter.Dispose();
        if (serverTask != null) {
            await serverTask.WaitAsync(timeout).ConfigureAwait(false);
            serverTask.Dispose();
        }
        clientReader.Dispose();
    }

    [Test]
    public async Task InitializeTest() {
        InitializeParams? initializedParams = null;
        server.ServerInfo = new ServerInfo { Name = "TestServer", Version = "1.0" };
        server.AddHandler(new TestHoverHandler());
        server.OnInitialized(parameters => {
            initializedParams = parameters;
            return Task.CompletedTask;
        });
        serverTask = server.RunAsync();

        await SendAsync(@"{""jsonrpc"":""2.0"",""id"":0,""method"":""initialize"",""params"":{""processId"":null,""clientInfo"":{""name"":""Visual Studio Code"",""version"":""1.100.0""},""locale"":""en"",""rootPath"":""/workspace"",""rootUri"":""file:///workspace"",""capabilities"":{""workspace"":{""applyEdit"":true}},""trace"":""off"",""workspaceFolders"":[{""uri"":""file:///workspace"",""name"":""workspace""}]}}").ConfigureAwait(false);
        var response = await ReceiveAsync().ConfigureAwait(false);

        Assert.That(response.GetProperty("id").GetInt32(), Is.EqualTo(0));
        Assert.That(response.GetProperty("result").GetProperty("capabilities").GetProperty("hoverProvider").GetBoolean(), Is.True);
        Assert.That(response.GetProperty("result").GetProperty("serverInfo").GetProperty("name").GetString(), Is.EqualTo("TestServer"));
        Assert.That(response.GetProperty("result").GetProperty("serverInfo").GetProperty("version").GetString(), Is.EqualTo("1.0"));

        await SendAsync(@"{""jsonrpc"":""2.0"",""method"":""initialized"",""params"":{}}").ConfigureAwait(false);
        await SendAsync(@"{""jsonrpc"":""2.0"",""id"":1,""method"":""textDocument/hover"",""params"":{""textDocument"":{""uri"":""file:///workspace/Test.cs""},""position"":{""line"":3,""character"":7}}}").ConfigureAwait(false);
        response = await ReceiveAsync().ConfigureAwait(false);

        Assert.That(response.GetProperty("id").GetInt32(), Is.EqualTo(1));
        Assert.That(response.GetProperty("result").GetProperty("contents").GetProperty("kind").GetString(), Is.EqualTo("markdown"));
        Assert.That(response.GetProperty("result").GetProperty("contents").GetProperty("value").GetString(), Is.EqualTo("Test.cs:3:7"));
        Assert.That(initializedParams, Is.Not.Null);
        Assert.That(initializedParams.ClientInfo?.Name, Is.EqualTo("Visual Studio Code"));
        Assert.That(initializedParams.WorkspaceFolders, Has.Count.EqualTo(1));
        Assert.That(initializedParams.WorkspaceFolders?[0].Name, Is.EqualTo("workspace"));
    }
    [Test]
    public async Task RequestBeforeInitializeTest() {
        server.AddHandler(new TestHoverHandler());
        serverTask = server.RunAsync();

        await SendAsync(@"{""jsonrpc"":""2.0"",""id"":1,""method"":""textDocument/hover"",""params"":{""textDocument"":{""uri"":""file:///workspace/Test.cs""},""position"":{""line"":0,""character"":0}}}").ConfigureAwait(false);
        var response = await ReceiveAsync().ConfigureAwait(false);

        Assert.That(response.GetProperty("error").GetProperty("code").GetInt32(), Is.EqualTo(ErrorCodes.ServerNotInitialized));
        Assert.That(response.TryGetProperty("result", out _), Is.False);
    }
    [Test]
    public async Task ShutdownAndExitTest() {
        var isShutdownHandled = false;
        server.AddRequestHandler<JsonElement, string>("test/request", (_, _) => Task.FromResult("ok"));
        server.OnShutdown(() => {
            isShutdownHandled = true;
            return Task.CompletedTask;
        });
        await InitializeAsync().ConfigureAwait(false);

        await SendAsync(@"{""jsonrpc"":""2.0"",""id"":1,""method"":""shutdown""}").ConfigureAwait(false);
        var response = await ReceiveAsync().ConfigureAwait(false);

        Assert.That(response.GetRawText(), Is.EqualTo(@"{""jsonrpc"":""2.0"",""id"":1,""result"":null}"));
        Assert.That(isShutdownHandled, Is.True);

        await SendAsync(@"{""jsonrpc"":""2.0"",""id"":2,""method"":""test/request"",""params"":{}}").ConfigureAwait(false);
        response = await ReceiveAsync().ConfigureAwait(false);
        Assert.That(response.GetProperty("error").GetProperty("code").GetInt32(), Is.EqualTo(ErrorCodes.InvalidRequest));

        await SendAsync(@"{""jsonrpc"":""2.0"",""method"":""exit""}").ConfigureAwait(false);
        Assert.That(await serverTask!.WaitAsync(timeout).ConfigureAwait(false), Is.EqualTo(0));
    }
    [Test]
    public async Task ExitWithoutShutdownTest() {
        await InitializeAsync().ConfigureAwait(false);

        await SendAsync(@"{""jsonrpc"":""2.0"",""method"":""exit""}").ConfigureAwait(false);
        Assert.That(await serverTask!.WaitAsync(timeout).ConfigureAwait(false), Is.EqualTo(1));
    }
    [Test]
    public async Task InputClosedTest() {
        await InitializeAsync().ConfigureAwait(false);

        clientWriter.Dispose();
        Assert.That(await serverTask!.WaitAsync(timeout).ConfigureAwait(false), Is.EqualTo(1));
    }
    [Test]
    public async Task UnknownMethodTest() {
        await InitializeAsync().ConfigureAwait(false);

        await SendAsync(@"{""jsonrpc"":""2.0"",""method"":""test/unknownNotification"",""params"":{}}").ConfigureAwait(false);
        await SendAsync(@"{""jsonrpc"":""2.0"",""id"":""abc"",""method"":""test/unknownRequest"",""params"":{}}").ConfigureAwait(false);
        var response = await ReceiveAsync().ConfigureAwait(false);

        Assert.That(response.GetProperty("id").GetString(), Is.EqualTo("abc"));
        Assert.That(response.GetProperty("error").GetProperty("code").GetInt32(), Is.EqualTo(ErrorCodes.MethodNotFound));
    }
    [Test]
    public async Task InvalidParamsTest() {
        server.AddHandler(new TestHoverHandler());
        await InitializeAsync().ConfigureAwait(false);

        await SendAsync(@"{""jsonrpc"":""2.0"",""id"":1,""method"":""textDocument/hover"",""params"":{""textDocument"":{""uri"":42}}}").ConfigureAwait(false);
        var response = await ReceiveAsync().ConfigureAwait(false);
        Assert.That(response.GetProperty("error").GetProperty("code").GetInt32(), Is.EqualTo(ErrorCodes.InvalidParams));

        await SendAsync(@"{""jsonrpc"":""2.0"",""id"":2,""method"":""textDocument/hover""}").ConfigureAwait(false);
        response = await ReceiveAsync().ConfigureAwait(false);
        Assert.That(response.GetProperty("error").GetProperty("code").GetInt32(), Is.EqualTo(ErrorCodes.InvalidParams));
    }
    [Test]
    public async Task HandlerExceptionTest() {
        server.AddRequestHandler<JsonElement, string>("test/request", (_, _) => throw new InvalidOperationException("Test failure"));
        await InitializeAsync().ConfigureAwait(false);

        await SendAsync(@"{""jsonrpc"":""2.0"",""id"":1,""method"":""test/request"",""params"":{}}").ConfigureAwait(false);
        var response = await ReceiveAsync().ConfigureAwait(false);

        Assert.That(response.GetProperty("error").GetProperty("code").GetInt32(), Is.EqualTo(ErrorCodes.InternalError));
        Assert.That(response.GetProperty("error").GetProperty("message").GetString(), Is.EqualTo("Test failure"));
    }
    [Test]
    public async Task NullResultTest() {
        server.AddRequestHandler<JsonElement, Hover?>("test/request", (_, _) => Task.FromResult<Hover?>(null));
        await InitializeAsync().ConfigureAwait(false);

        await SendAsync(@"{""jsonrpc"":""2.0"",""id"":1,""method"":""test/request"",""params"":{}}").ConfigureAwait(false);
        var response = await ReceiveAsync().ConfigureAwait(false);

        Assert.That(response.GetRawText(), Is.EqualTo(@"{""jsonrpc"":""2.0"",""id"":1,""result"":null}"));
    }
    [Test]
    public async Task InvalidMessageTest() {
        server.AddRequestHandler<JsonElement, string>("test/request", (_, _) => Task.FromResult("ok"));
        await InitializeAsync().ConfigureAwait(false);

        await SendAsync(@"{""jsonrpc"":""2.0"",").ConfigureAwait(false);
        await SendAsync(@"{""jsonrpc"":""2.0"",""id"":1,""method"":""test/request"",""params"":{}}").ConfigureAwait(false);
        var response = await ReceiveAsync().ConfigureAwait(false);

        Assert.That(response.GetProperty("id").GetInt32(), Is.EqualTo(1));
        Assert.That(response.GetProperty("result").GetString(), Is.EqualTo("ok"));
    }
    [Test]
    public async Task SplitMessageTest() {
        server.AddRequestHandler<JsonElement, string?>("test/request", (parameters, _) => Task.FromResult(parameters.GetProperty("text").GetString()));
        await InitializeAsync().ConfigureAwait(false);

        var largeText = new string('я', 100000);
        var content = Encoding.UTF8.GetBytes($@"{{""jsonrpc"":""2.0"",""id"":1,""method"":""test/request"",""params"":{{""text"":""{largeText}""}}}}");
        var message = Encoding.ASCII.GetBytes($"Content-Type: application/vscode-jsonrpc; charset=utf-8\r\ncontent-length: {content.Length}\r\n\r\n").Concat(content).ToArray();
        for (int offset = 0; offset < message.Length; offset += 7000) {
            await clientWriter.WriteAsync(message.AsMemory(offset, Math.Min(7000, message.Length - offset))).ConfigureAwait(false);
            await clientWriter.FlushAsync().ConfigureAwait(false);
        }
        var response = await ReceiveAsync().ConfigureAwait(false);

        Assert.That(response.GetProperty("result").GetString(), Is.EqualTo(largeText));
    }
    [Test]
    public async Task CancelRequestTest() {
        var requestStarted = new TaskCompletionSource();
        server.Dispatcher = new ConcurrentDispatcher();
        server.AddRequestHandler<JsonElement, string>("test/request", async (_, token) => {
            requestStarted.TrySetResult();
            await Task.Delay(Timeout.Infinite, token).ConfigureAwait(false);
            return "completed";
        });
        await InitializeAsync().ConfigureAwait(false);

        await SendAsync(@"{""jsonrpc"":""2.0"",""id"":5,""method"":""test/request"",""params"":{}}").ConfigureAwait(false);
        await requestStarted.Task.WaitAsync(timeout).ConfigureAwait(false);
        await SendAsync(@"{""jsonrpc"":""2.0"",""method"":""$/cancelRequest"",""params"":{""id"":5}}").ConfigureAwait(false);
        var response = await ReceiveAsync().ConfigureAwait(false);

        Assert.That(response.GetProperty("id").GetInt32(), Is.EqualTo(5));
        Assert.That(response.GetProperty("error").GetProperty("code").GetInt32(), Is.EqualTo(ErrorCodes.RequestCancelled));
    }
    [Test]
    public async Task CancelQueuedRequestTest() {
        var releaseFirstRequest = new TaskCompletionSource();
        var isSecondRequestHandled = false;
        server.AddRequestHandler<JsonElement, string>("test/first", async (_, _) => {
            await releaseFirstRequest.Task.ConfigureAwait(false);
            return "first";
        });
        server.AddRequestHandler<JsonElement, string>("test/second", (_, token) => {
            isSecondRequestHandled = !token.IsCancellationRequested;
            return Task.FromResult("second");
        });
        await InitializeAsync().ConfigureAwait(false);

        await SendAsync(@"{""jsonrpc"":""2.0"",""id"":1,""method"":""test/first"",""params"":{}}").ConfigureAwait(false);
        await SendAsync(@"{""jsonrpc"":""2.0"",""id"":""second"",""method"":""test/second"",""params"":{}}").ConfigureAwait(false);
        await SendAsync(@"{""jsonrpc"":""2.0"",""method"":""$/cancelRequest"",""params"":{""id"":""second""}}").ConfigureAwait(false);
        await SendAsync(@"{""jsonrpc"":""2.0"",""id"":3,""method"":""test/first"",""params"":{}}").ConfigureAwait(false);
        await Task.Delay(200).ConfigureAwait(false);
        releaseFirstRequest.SetResult();

        var response = await ReceiveAsync().ConfigureAwait(false);
        Assert.That(response.GetProperty("id").GetInt32(), Is.EqualTo(1));
        response = await ReceiveAsync().ConfigureAwait(false);
        Assert.That(response.GetProperty("id").GetString(), Is.EqualTo("second"));
        Assert.That(response.GetProperty("error").GetProperty("code").GetInt32(), Is.EqualTo(ErrorCodes.RequestCancelled));
        Assert.That(isSecondRequestHandled, Is.False);
        response = await ReceiveAsync().ConfigureAwait(false);
        Assert.That(response.GetProperty("id").GetInt32(), Is.EqualTo(3));
    }
    [Test]
    public async Task ExclusiveMessageTest() {
        var requestStarted = new TaskCompletionSource();
        var releaseRequest = new TaskCompletionSource();
        var notificationHandled = new TaskCompletionSource<string>();
        var isRequestCompleted = false;
        server.Dispatcher = new ConcurrentDispatcher();
        server.AddRequestHandler<JsonElement, string>("test/request", async (_, _) => {
            requestStarted.TrySetResult();
            await releaseRequest.Task.ConfigureAwait(false);
            isRequestCompleted = true;
            return "completed";
        });
        server.AddNotificationHandler<DidOpenTextDocumentParams>("test/exclusive", (request, _) => {
            notificationHandled.TrySetResult($"{isRequestCompleted}:{request.TextDocument.Text}");
            return Task.CompletedTask;
        });
        await InitializeAsync().ConfigureAwait(false);

        await SendAsync(@"{""jsonrpc"":""2.0"",""id"":1,""method"":""test/request"",""params"":{}}").ConfigureAwait(false);
        await requestStarted.Task.WaitAsync(timeout).ConfigureAwait(false);
        await SendAsync(@"{""jsonrpc"":""2.0"",""method"":""test/exclusive"",""params"":{""textDocument"":{""uri"":""file:///workspace/Test.cs"",""languageId"":""csharp"",""version"":1,""text"":""class Test {}""}}}").ConfigureAwait(false);
        await Task.Delay(200).ConfigureAwait(false);
        Assert.That(notificationHandled.Task.IsCompleted, Is.False);

        releaseRequest.SetResult();
        Assert.That(await notificationHandled.Task.WaitAsync(timeout).ConfigureAwait(false), Is.EqualTo("True:class Test {}"));
        var response = await ReceiveAsync().ConfigureAwait(false);
        Assert.That(response.GetProperty("result").GetString(), Is.EqualTo("completed"));
    }
    [Test]
    public async Task ConcurrentRequestsTest() {
        var releaseFirstRequest = new TaskCompletionSource();
        server.Dispatcher = new ConcurrentDispatcher();
        server.AddRequestHandler<JsonElement, string>("test/first", async (_, _) => {
            await releaseFirstRequest.Task.ConfigureAwait(false);
            return "first";
        });
        server.AddRequestHandler<JsonElement, string>("test/second", (_, _) => Task.FromResult("second"));
        await InitializeAsync().ConfigureAwait(false);

        await SendAsync(@"{""jsonrpc"":""2.0"",""id"":1,""method"":""test/first"",""params"":{}}").ConfigureAwait(false);
        await SendAsync(@"{""jsonrpc"":""2.0"",""id"":2,""method"":""test/second"",""params"":{}}").ConfigureAwait(false);
        var response = await ReceiveAsync().ConfigureAwait(false);
        Assert.That(response.GetProperty("result").GetString(), Is.EqualTo("second"));

        releaseFirstRequest.SetResult();
        response = await ReceiveAsync().ConfigureAwait(false);
        Assert.That(response.GetProperty("result").GetString(), Is.EqualTo("first"));
    }
    [Test]
    public async Task ClientRequestTest() {
        await InitializeAsync().ConfigureAwait(false);

        var requestTask = server.Client.SendRequestAsync<WorkspaceFolder>("test/clientRequest", new Position(1, 2), CancellationToken.None);
        var request = await ReceiveAsync().ConfigureAwait(false);
        Assert.That(request.GetProperty("method").GetString(), Is.EqualTo("test/clientRequest"));
        Assert.That(request.GetProperty("params").GetRawText(), Is.EqualTo(@"{""line"":1,""character"":2}"));

        await SendAsync($@"{{""jsonrpc"":""2.0"",""id"":{request.GetProperty("id").GetRawText()},""result"":{{""uri"":""file:///workspace"",""name"":""workspace""}}}}").ConfigureAwait(false);
        var result = await requestTask.WaitAsync(timeout).ConfigureAwait(false);
        Assert.That(result?.Name, Is.EqualTo("workspace"));

        var failedTask = server.Client.RefreshSemanticTokensAsync(CancellationToken.None);
        request = await ReceiveAsync().ConfigureAwait(false);
        Assert.That(request.GetProperty("method").GetString(), Is.EqualTo("workspace/semanticTokens/refresh"));
        Assert.That(request.TryGetProperty("params", out _), Is.False);

        await SendAsync($@"{{""jsonrpc"":""2.0"",""id"":{request.GetProperty("id").GetRawText()},""error"":{{""code"":-32601,""message"":""Unhandled method""}}}}").ConfigureAwait(false);
        var exception = Assert.ThrowsAsync<JsonRpcException>(() => failedTask.WaitAsync(timeout));
        Assert.That(exception.Code, Is.EqualTo(ErrorCodes.MethodNotFound));
        Assert.That(exception.Message, Is.EqualTo("Unhandled method"));
    }
    [Test]
    public async Task ProgressTest() {
        await InitializeAsync().ConfigureAwait(false);

        var progressTask = server.Client.CreateProgressAsync("Loading", CancellationToken.None);
        var request = await ReceiveAsync().ConfigureAwait(false);
        var token = request.GetProperty("params").GetProperty("token").GetString();
        Assert.That(request.GetProperty("method").GetString(), Is.EqualTo("window/workDoneProgress/create"));
        Assert.That(token, Is.Not.Empty);

        await SendAsync($@"{{""jsonrpc"":""2.0"",""id"":{request.GetProperty("id").GetRawText()},""result"":null}}").ConfigureAwait(false);
        var notification = await ReceiveAsync().ConfigureAwait(false);
        Assert.That(notification.GetRawText(), Is.EqualTo($@"{{""jsonrpc"":""2.0"",""method"":""$/progress"",""params"":{{""token"":""{token}"",""value"":{{""kind"":""begin"",""title"":""Loading""}}}}}}"));

        var progress = await progressTask.WaitAsync(timeout).ConfigureAwait(false);
        progress.Report("Restoring Test", 50);
        notification = await ReceiveAsync().ConfigureAwait(false);
        Assert.That(notification.GetRawText(), Is.EqualTo($@"{{""jsonrpc"":""2.0"",""method"":""$/progress"",""params"":{{""token"":""{token}"",""value"":{{""kind"":""report"",""message"":""Restoring Test"",""percentage"":50}}}}}}"));

        progress.End();
        notification = await ReceiveAsync().ConfigureAwait(false);
        Assert.That(notification.GetRawText(), Is.EqualTo($@"{{""jsonrpc"":""2.0"",""method"":""$/progress"",""params"":{{""token"":""{token}"",""value"":{{""kind"":""end""}}}}}}"));
    }

    private async Task InitializeAsync() {
        serverTask = server.RunAsync();
        await SendAsync(@"{""jsonrpc"":""2.0"",""id"":0,""method"":""initialize"",""params"":{""processId"":null,""capabilities"":{}}}").ConfigureAwait(false);
        await ReceiveAsync().ConfigureAwait(false);
        await SendAsync(@"{""jsonrpc"":""2.0"",""method"":""initialized"",""params"":{}}").ConfigureAwait(false);
    }
    private async Task SendAsync(string json) {
        var content = Encoding.UTF8.GetBytes(json);
        await clientWriter.WriteAsync(Encoding.ASCII.GetBytes($"Content-Length: {content.Length}\r\n\r\n")).ConfigureAwait(false);
        await clientWriter.WriteAsync(content).ConfigureAwait(false);
        await clientWriter.FlushAsync().ConfigureAwait(false);
    }
    private async Task<JsonElement> ReceiveAsync() {
        var headers = new StringBuilder();
        var buffer = new byte[1];
        while (!headers.ToString().EndsWith("\r\n\r\n", StringComparison.Ordinal)) {
            await clientReader.ReadExactlyAsync(buffer).AsTask().WaitAsync(timeout).ConfigureAwait(false);
            headers.Append((char)buffer[0]);
        }

        Assert.That(headers.ToString(), Does.StartWith("Content-Length: "));
        var content = new byte[int.Parse(headers.ToString().Substring("Content-Length: ".Length).Trim())];
        await clientReader.ReadExactlyAsync(content).AsTask().WaitAsync(timeout).ConfigureAwait(false);
        using var document = JsonDocument.Parse(content);
        return document.RootElement.Clone();
    }

    private class ConcurrentDispatcher : MessageDispatcher {
        public override bool IsExclusive(string method) {
            return method == "test/exclusive";
        }
    }
    private class TestHoverHandler : DotRush.Protocol.Handlers.HoverHandlerBase {
        public override void RegisterCapability(ServerCapabilities serverCapabilities) {
            serverCapabilities.HoverProvider = true;
        }
        protected override Task<Hover?> Handle(HoverParams request, CancellationToken token) {
            return Task.FromResult<Hover?>(new Hover {
                Contents = new MarkupContent { Value = $"{Path.GetFileName(request.TextDocument.Uri.FileSystemPath)}:{request.Position.Line}:{request.Position.Character}" }
            });
        }
    }
}
