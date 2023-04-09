using LanguageServer;
using LanguageServer.Parameters.General;
using LanguageServer.Parameters.TextDocument;

namespace dotRush.Server;

public abstract class Session : ServiceConnection {
    protected Session(Stream input, Stream output) : base(input, output) {}

    protected override Result<InitializeResult, ResponseError<InitializeErrorData>> Initialize(InitializeParams @params) {
        var result = new InitializeResult {
            capabilities = new ServerCapabilities {
                //codeActionProvider = true,
                referencesProvider = true,
                definitionProvider = true,
                implementationProvider = true,
                textDocumentSync = TextDocumentSyncKind.Incremental,
                completionProvider = new CompletionOptions {
                    triggerCharacters = new[] { ".", ":", " " },
                    resolveProvider = true,
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