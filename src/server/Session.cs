using LanguageServer;
using LanguageServer.Parameters.General;

namespace dotRush.Server;

public abstract class Session : ServiceConnection {
    protected Session(Stream input, Stream output) : base(input, output) {}

    protected override Result<InitializeResult, ResponseError<InitializeErrorData>> Initialize(InitializeParams @params) {
        var result = new InitializeResult {
            capabilities = new ServerCapabilities {
                textDocumentSync = TextDocumentSyncKind.Incremental,
                completionProvider = new CompletionOptions {
                    triggerCharacters = new[] { ".", ":" },
                }
            }
        };
        return Result<InitializeResult, ResponseError<InitializeErrorData>>.Success(result);
    }

    protected override VoidResult<ResponseError> Shutdown() {
        Task.Delay(1000).ContinueWith(_ => Environment.Exit(0));
        return VoidResult<ResponseError>.Success();
    }
}