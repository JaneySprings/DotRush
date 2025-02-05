using System.Diagnostics;
using System.Reflection;
using DotRush.Roslyn.Common.Logging;
using DotRush.Roslyn.Server.Extensions;
using DotRush.Roslyn.Server.Services;
using EmmyLua.LanguageServer.Framework.Protocol.Message.Initialize;
using EmmyLua.LanguageServer.Framework.Server;
using EmmyLuaLanguageServer = EmmyLua.LanguageServer.Framework.Server.LanguageServer;

namespace DotRush.Roslyn.Server;

public class LanguageServer {
    private static EmmyLuaLanguageServer server = null!;
    private static InitializeParams initializeParameters = null!;

    private static ConfigurationService configurationService = null!;
    private static WorkspaceService workspaceService = null!;
    private static CodeAnalysisService codeAnalysisService = null!;
    private static NavigationService navigationService = null!;
    private static ExternalAccessService externalAccessService = null!;

    public static ClientProxy Proxy => server.Client;
    public static EmmyLuaLanguageServer Server => server;
    // public static TextDocumentSelector SelectorForAllDocuments => TextDocumentSelector.ForPattern("**/*.cs", "**/*.xaml");
    // public static TextDocumentSelector SelectorForSourceCodeDocuments => TextDocumentSelector.ForPattern("**/*.cs");

    public static Task Main(string[] args) {
        server = EmmyLuaLanguageServer
            .From(Console.OpenStandardInput(), Console.OpenStandardOutput());
            // .WithHandler<DidOpenTextDocumentHandler>()
            // .WithHandler<DidChangeTextDocumentHandler>()
            // .WithHandler<DidCloseTextDocumentHandler>()
            // .WithHandler<DidChangeWatchedFilesHandler>()
            // .WithHandler<WorkspaceSymbolsHandler>()
            // .WithHandler<DocumentSymbolHandler>()
            // .WithHandler<HoverHandler>()
            // .WithHandler<FoldingRangeHandler>()5
            // .WithHandler<SignatureHelpHandler>()
            // .WithHandler<FormattingHandler>()
            // .WithHandler<RangeFormattingHandler>()
            // .WithHandler<RenameHandler>()
            // .WithHandler<CompletionHandler>()
            // .WithHandler<CodeActionHandler>()
            // .WithHandler<ReferencesHandler>()
            // .WithHandler<ImplementationHandler>()
            // .WithHandler<DefinitionHandler>()
            // .WithHandler<TypeDefinitionHandler>()

        server.OnInitialize(OnInitializeAsync);
        server.OnInitialized(OnInitializedAsync);
        return server.Run();
    }
    private static Task OnInitializeAsync(InitializeParams parameters, ServerInfo serverInfo) {
        initializeParameters = parameters;
        ObserveClientProcess(parameters.ProcessId);
        ConfigureServerInfo(serverInfo);
        ConfigureServices();
        return Task.CompletedTask;
    }
    private static async Task OnInitializedAsync(InitializedParams parameters) {
        await configurationService.InitializeAsync().ConfigureAwait(false);
        if (!workspaceService.InitializeWorkspace())
            LanguageServer.Proxy.ShowError(Resources.DotNetRegistrationFailed);

        workspaceService.WorkspaceStateChanged += (_, _) => navigationService.UpdateSolution(workspaceService.Solution);
        _ = workspaceService.LoadAsync(initializeParameters.WorkspaceFolders, CancellationToken.None);
        _ = externalAccessService.StartListeningAsync(initializeParameters.ProcessId, CancellationToken.None);
    }
 
    private static void ObserveClientProcess(int? pid) {
        if (pid == null || pid <= 0)
            return;

        var ideProcess = Process.GetProcessById(pid.Value);
        ideProcess.EnableRaisingEvents = true;
        ideProcess.Exited += (_, _) => {
            CurrentSessionLogger.Debug($"Shutting down server because client process has exited");
            Environment.Exit(0);
        };
        CurrentSessionLogger.Debug($"Server is observing client process {ideProcess.ProcessName} (PID: {pid})");
    }
    private static void ConfigureServerInfo(ServerInfo serverInfo) {
        serverInfo.Version = Assembly.GetExecutingAssembly().GetName().Version?.ToString();
        serverInfo.Name  = Assembly.GetExecutingAssembly().GetName().Name ?? string.Empty;
    }
    private static void ConfigureServices() {
        configurationService = new ConfigurationService();
        navigationService = new NavigationService();
        workspaceService = new WorkspaceService(configurationService);
        codeAnalysisService = new CodeAnalysisService(configurationService, workspaceService);
        externalAccessService = new ExternalAccessService(workspaceService);
    }
}
