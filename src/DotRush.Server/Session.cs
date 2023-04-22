using DotRush.Server.Handlers;
using LanguageServer;
using LanguageServer.Parameters.General;

namespace DotRush.Server;

public abstract class Session : ServiceConnection {
    protected Session(Stream input, Stream output) : base(input, output) {
        NotificationHandlers.Set<FrameworkChangedArgs>("frameworkChanged", FrameworkChanged);
        NotificationHandlers.Set<ReloadTargetsArgs>("reloadTargets", ReloadTargets);
    }

    protected override Result<InitializeResult, ResponseError<InitializeErrorData>> Initialize(InitializeParams @params) {
        var result = new InitializeResult {
            capabilities = new ServerCapabilities {
                //codeActionProvider = true,
                renameProvider = true,
                referencesProvider = true,
                definitionProvider = true,
                implementationProvider = true,
                documentFormattingProvider = true,
                documentRangeFormattingProvider = true,
                textDocumentSync = TextDocumentSyncKind.Full,
                completionProvider = new CompletionOptions {
                    triggerCharacters = new[] { ".", ":", " " },
                    resolveProvider = true,
                },
                workspace = new WorkspaceOptions {
                    workspaceFolders = new WorkspaceFoldersOptions {
                        changeNotifications = true,
                        supported = true
                    },
                }
            }
        };
        return Result<InitializeResult, ResponseError<InitializeErrorData>>.Success(result);
    }

    protected override VoidResult<ResponseError> Shutdown() {
        Task.Delay(1000).ContinueWith(_ => Environment.Exit(0));
        return VoidResult<ResponseError>.Success();
    }

    protected abstract void FrameworkChanged(FrameworkChangedArgs args);
    protected abstract void ReloadTargets(ReloadTargetsArgs args);
}