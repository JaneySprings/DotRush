using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using System.Threading.Channels;
using DotRush.Common.Logging;
using DotRush.Protocol.Handlers;
using DotRush.Protocol.JsonRpc;
using DotRush.Protocol.Models;

namespace DotRush.Protocol;

public class LanguageServer {
    private const int StateCreated = 0;
    private const int StateInitialized = 1;
    private const int StateShutdown = 2;
    private static readonly TimeSpan exitTimeout = TimeSpan.FromSeconds(5);

    private readonly JsonRpcConnection connection;
    private readonly List<IHandler> handlers;
    private readonly Dictionary<string, Func<JsonElement?, CancellationToken, Task<object?>>> requestHandlers;
    private readonly Dictionary<string, Func<JsonElement?, CancellationToken, Task>> notificationHandlers;
    private readonly List<Func<InitializeParams, Task>> initializedCallbacks;
    private readonly List<Func<Task>> shutdownCallbacks;
    private readonly ConcurrentDictionary<RequestId, CancellationTokenSource> incomingRequests;
    private readonly ConcurrentDictionary<long, TaskCompletionSource<JsonElement?>> outgoingRequests;
    private readonly Channel<JsonRpcMessage> messageQueue;
    private readonly CancellationTokenSource lifetimeTokenSource;
    private readonly TaskCompletionSource exitTaskSource;
    private volatile MessageDispatcher dispatcher;
    private volatile int state;
    private volatile bool isShutdownRequested;
    private Process? clientProcess;
    private long lastRequestId;
    private int isRunning;

    public LanguageClient Client { get; }
    public ServerInfo? ServerInfo { get; set; }
    public InitializeParams? InitializeParams { get; private set; }
    public MessageDispatcher Dispatcher {
        get => dispatcher;
        set => dispatcher = value;
    }

    public LanguageServer(Stream input, Stream output) {
        connection = new JsonRpcConnection(input, output);
        handlers = new List<IHandler>();
        requestHandlers = new Dictionary<string, Func<JsonElement?, CancellationToken, Task<object?>>>();
        notificationHandlers = new Dictionary<string, Func<JsonElement?, CancellationToken, Task>>();
        initializedCallbacks = new List<Func<InitializeParams, Task>>();
        shutdownCallbacks = new List<Func<Task>>();
        incomingRequests = new ConcurrentDictionary<RequestId, CancellationTokenSource>();
        outgoingRequests = new ConcurrentDictionary<long, TaskCompletionSource<JsonElement?>>();
        messageQueue = Channel.CreateUnbounded<JsonRpcMessage>(new UnboundedChannelOptions { SingleReader = true, SingleWriter = true });
        lifetimeTokenSource = new CancellationTokenSource();
        exitTaskSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        dispatcher = new MessageDispatcher();
        Client = new LanguageClient(this);

        requestHandlers[Methods.Initialize] = HandleInitializeAsync;
        requestHandlers[Methods.Shutdown] = HandleShutdownAsync;
        notificationHandlers[Methods.Initialized] = HandleInitializedAsync;
    }

    public LanguageServer AddHandler(IHandler handler) {
        handlers.Add(handler);
        handler.RegisterHandler(this);
        return this;
    }
    public void AddRequestHandler<TParams, TResult>(string method, Func<TParams, CancellationToken, Task<TResult>> handler) {
        requestHandlers[method] = async (parameters, token) => await handler.Invoke(DeserializeParams<TParams>(method, parameters), token).ConfigureAwait(false);
    }
    public void AddNotificationHandler<TParams>(string method, Func<TParams, CancellationToken, Task> handler) {
        notificationHandlers[method] = (parameters, token) => handler.Invoke(DeserializeParams<TParams>(method, parameters), token);
    }
    public void AddNotificationHandler(string method, Func<CancellationToken, Task> handler) {
        notificationHandlers[method] = (_, token) => handler.Invoke(token);
    }
    public void OnInitialized(Func<InitializeParams, Task> callback) {
        initializedCallbacks.Add(callback);
    }
    public void OnShutdown(Func<Task> callback) {
        shutdownCallbacks.Add(callback);
    }

    // Completes when the client sends the 'exit' notification, closes the input stream or terminates.
    // Returns the process exit code required by the protocol: 0 if 'shutdown' was requested, otherwise 1.
    public async Task<int> RunAsync() {
        if (Interlocked.Exchange(ref isRunning, 1) != 0)
            throw new InvalidOperationException("Language server is already running.");

        var dispatchTask = Task.Run(DispatchMessagesAsync);
        _ = Task.Run(ReadMessagesAsync);
        await exitTaskSource.Task.ConfigureAwait(false);

        messageQueue.Writer.TryComplete();
        lifetimeTokenSource.Cancel();
        await Task.WhenAny(dispatchTask, Task.Delay(exitTimeout)).ConfigureAwait(false);
        return isShutdownRequested ? 0 : 1;
    }

    internal void SendNotification(string method, object? parameters) {
        connection.SendNotification(method, parameters);
    }
    internal async Task<TResult?> SendRequestAsync<TResult>(string method, object? parameters, CancellationToken cancellationToken) {
        var id = Interlocked.Increment(ref lastRequestId);
        var resultSource = new TaskCompletionSource<JsonElement?>(TaskCreationOptions.RunContinuationsAsynchronously);
        outgoingRequests[id] = resultSource;
        try {
            using var tokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifetimeTokenSource.Token);
            using var registration = tokenSource.Token.Register(() => resultSource.TrySetCanceled());
            connection.SendRequest(id, method, parameters);

            var result = await resultSource.Task.ConfigureAwait(false);
            if (result == null || result.Value.ValueKind == JsonValueKind.Null || result.Value.ValueKind == JsonValueKind.Undefined)
                return default;

            return result.Value.Deserialize<TResult>(ProtocolSerializer.Options);
        } finally {
            outgoingRequests.TryRemove(id, out _);
        }
    }

    private async Task ReadMessagesAsync() {
        try {
            while (true) {
                JsonRpcMessage? message;
                try {
                    message = await connection.ReadAsync(lifetimeTokenSource.Token).ConfigureAwait(false);
                } catch (JsonException e) {
                    CurrentSessionLogger.Error($"Invalid message received: {e.Message}");
                    continue;
                }

                if (message == null) {
                    CurrentSessionLogger.Debug("Shutting down server because input stream has been closed");
                    break;
                }
                if (!AcceptMessage(message))
                    break;
            }
        } catch (OperationCanceledException) {
        } catch (Exception e) {
            CurrentSessionLogger.Error(e);
        } finally {
            exitTaskSource.TrySetResult();
        }
    }
    // Cancellation and responses must not wait for the handlers in the queue, so they are processed by the reader
    private bool AcceptMessage(JsonRpcMessage message) {
        if (message.IsResponse) {
            CompleteOutgoingRequest(message);
            return true;
        }
        if (message.Method == null)
            return true;

        switch (message.Method) {
            case Methods.Exit:
                return false;
            case Methods.CancelRequest:
                CancelIncomingRequest(message.Params);
                return true;
            case Methods.Shutdown:
                isShutdownRequested = true;
                foreach (var tokenSource in incomingRequests.Values)
                    Cancel(tokenSource);
                break;
        }

        if (message.Id != null)
            incomingRequests[message.Id.Value] = CancellationTokenSource.CreateLinkedTokenSource(lifetimeTokenSource.Token);

        messageQueue.Writer.TryWrite(message);
        return true;
    }
    private void CompleteOutgoingRequest(JsonRpcMessage message) {
        var id = message.Id!.Value;
        if (id.Text != null || !outgoingRequests.TryRemove(id.Number, out var resultSource))
            return;

        if (message.Error != null)
            resultSource.TrySetException(new JsonRpcException(message.Error.Code, message.Error.Message));
        else
            resultSource.TrySetResult(message.Result);
    }
    private void CancelIncomingRequest(JsonElement? parameters) {
        try {
            var cancelParams = parameters?.Deserialize<CancelParams>(ProtocolSerializer.Options);
            if (cancelParams != null && incomingRequests.TryGetValue(cancelParams.Id, out var tokenSource))
                Cancel(tokenSource);
        } catch (JsonException e) {
            CurrentSessionLogger.Error($"Invalid cancel request received: {e.Message}");
        }
    }

    private async Task DispatchMessagesAsync() {
        var runningTasks = new List<Task>();
        try {
            await foreach (var message in messageQueue.Reader.ReadAllAsync(lifetimeTokenSource.Token).ConfigureAwait(false)) {
                var currentDispatcher = dispatcher;
                if (Methods.IsLifecycle(message.Method!) || currentDispatcher.IsExclusive(message.Method!)) {
                    await Task.WhenAll(runningTasks).ConfigureAwait(false);
                    runningTasks.Clear();
                    await HandleMessageAsync(message, currentDispatcher).ConfigureAwait(false);
                }
                else {
                    runningTasks.RemoveAll(task => task.IsCompleted);
                    runningTasks.Add(Task.Run(() => HandleMessageAsync(message, currentDispatcher)));
                }
            }
        } catch (OperationCanceledException) {
        } catch (Exception e) {
            CurrentSessionLogger.Error(e);
        }
    }
    private Task HandleMessageAsync(JsonRpcMessage message, MessageDispatcher currentDispatcher) {
        if (message.Id != null)
            return HandleRequestAsync(message.Id.Value, message.Method!, message.Params, currentDispatcher);

        return HandleNotificationAsync(message.Method!, message.Params, currentDispatcher);
    }
    private async Task HandleRequestAsync(RequestId id, string method, JsonElement? parameters, MessageDispatcher currentDispatcher) {
        var token = incomingRequests.TryGetValue(id, out var tokenSource) ? tokenSource.Token : lifetimeTokenSource.Token;
        try {
            if (!requestHandlers.TryGetValue(method, out var handler)) {
                connection.SendError(id, ErrorCodes.MethodNotFound, $"Method '{method}' is not supported");
                return;
            }

            var currentState = state;
            if (currentState != (method == Methods.Initialize ? StateCreated : StateInitialized)) {
                if (currentState == StateCreated)
                    connection.SendError(id, ErrorCodes.ServerNotInitialized, "Server is not initialized");
                else
                    connection.SendError(id, ErrorCodes.InvalidRequest, $"Method '{method}' is not allowed in the current server state");
                return;
            }

            object? result = null;
            await currentDispatcher.InvokeAsync(method, async () => result = await handler.Invoke(parameters, token).ConfigureAwait(false)).ConfigureAwait(false);
            if (token.IsCancellationRequested)
                connection.SendError(id, ErrorCodes.RequestCancelled, "Request cancelled");
            else
                connection.SendResult(id, result);
        } catch (OperationCanceledException) {
            connection.SendError(id, ErrorCodes.RequestCancelled, "Request cancelled");
        } catch (JsonRpcException e) {
            CurrentSessionLogger.Error(e.Message);
            connection.SendError(id, e.Code, e.Message);
        } catch (Exception e) {
            CurrentSessionLogger.Error(e);
            connection.SendError(id, ErrorCodes.InternalError, e.Message);
        } finally {
            if (incomingRequests.TryRemove(id, out tokenSource))
                tokenSource.Dispose();
        }
    }
    private async Task HandleNotificationAsync(string method, JsonElement? parameters, MessageDispatcher currentDispatcher) {
        try {
            if (state != StateInitialized || !notificationHandlers.TryGetValue(method, out var handler))
                return;

            await currentDispatcher.InvokeAsync(method, () => handler.Invoke(parameters, lifetimeTokenSource.Token)).ConfigureAwait(false);
        } catch (OperationCanceledException) {
        } catch (JsonRpcException e) {
            CurrentSessionLogger.Error(e.Message);
        } catch (Exception e) {
            CurrentSessionLogger.Error(e);
        }
    }

    private Task<object?> HandleInitializeAsync(JsonElement? parameters, CancellationToken token) {
        var initializeParams = DeserializeParams<InitializeParams>(Methods.Initialize, parameters);
        var serverCapabilities = new ServerCapabilities();
        foreach (var handler in handlers)
            handler.RegisterCapability(serverCapabilities);

        ObserveClientProcess(initializeParams.ProcessId);
        InitializeParams = initializeParams;
        state = StateInitialized;
        return Task.FromResult<object?>(new InitializeResult { Capabilities = serverCapabilities, ServerInfo = ServerInfo });
    }
    private async Task HandleInitializedAsync(JsonElement? parameters, CancellationToken token) {
        foreach (var callback in initializedCallbacks)
            await callback.Invoke(InitializeParams!).ConfigureAwait(false);
    }
    private async Task<object?> HandleShutdownAsync(JsonElement? parameters, CancellationToken token) {
        state = StateShutdown;
        foreach (var callback in shutdownCallbacks)
            await callback.Invoke().ConfigureAwait(false);

        return null;
    }
    private void ObserveClientProcess(int? processId) {
        if (processId == null || processId <= 0)
            return;

        try {
            clientProcess = Process.GetProcessById(processId.Value);
            clientProcess.Exited += (_, _) => {
                CurrentSessionLogger.Debug("Shutting down server because client process has exited");
                exitTaskSource.TrySetResult();
            };
            clientProcess.EnableRaisingEvents = true;
            CurrentSessionLogger.Debug($"Server is observing client process (PID: {processId})");
        } catch (Exception e) {
            CurrentSessionLogger.Error($"Failed to observe client process (PID: {processId}): {e.Message}");
        }
    }

    private static TParams DeserializeParams<TParams>(string method, JsonElement? parameters) {
        if (parameters == null || parameters.Value.ValueKind == JsonValueKind.Null || parameters.Value.ValueKind == JsonValueKind.Undefined)
            throw new JsonRpcException(ErrorCodes.InvalidParams, $"Parameters of '{method}' are missing");

        try {
            return parameters.Value.Deserialize<TParams>(ProtocolSerializer.Options)!;
        } catch (JsonException e) {
            throw new JsonRpcException(ErrorCodes.InvalidParams, $"Parameters of '{method}' are invalid: {e.Message}");
        }
    }
    private static void Cancel(CancellationTokenSource tokenSource) {
        try {
            tokenSource.Cancel();
        } catch (ObjectDisposedException) {
            // The request has been completed
        }
    }
}
