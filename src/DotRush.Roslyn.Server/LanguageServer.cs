using System.Diagnostics;
using System.Reflection;
using DotRush.Common.Logging;
using DotRush.Roslyn.Server.Extensions;
using DotRush.Roslyn.Server.Handlers.Framework;
using DotRush.Roslyn.Server.Handlers.TextDocument;
using DotRush.Roslyn.Server.Handlers.Workspace;
using DotRush.Roslyn.Server.Services;
using EmmyLua.LanguageServer.Framework.Protocol.JsonRpc;
using EmmyLua.LanguageServer.Framework.Protocol.Message.Initialize;
using EmmyLua.LanguageServer.Framework.Server;
using EmmyLuaLanguageServer = EmmyLua.LanguageServer.Framework.Server.LanguageServer;

namespace DotRush.Roslyn.Server;

public class LanguageServer {
    private static EmmyLuaLanguageServer server = null!;

    private static ConfigurationService configurationService = null!;
    private static WorkspaceService workspaceService = null!;
    private static CodeAnalysisService codeAnalysisService = null!;
    private static NavigationService navigationService = null!;
    private static ExternalAccessService externalAccessService = null!;

    public static ClientProxy Proxy => server.Client;
    public static EmmyLuaLanguageServer Server => server;

    public static Task Main(string[] args) {
        Console.SetError(TextWriter.Null);
        Console.SetOut(TextWriter.Null);
        Console.SetIn(TextReader.Null);
        ConfigureServices();

        server = EmmyLuaLanguageServer.From(Console.OpenStandardInput(), Console.OpenStandardOutput());
        server.AddHandler(new TextDocumentHandler(workspaceService, codeAnalysisService))
              .AddHandler(new DocumentFormattingHandler(workspaceService))
              .AddHandler(new RenameHandler(workspaceService))
              .AddHandler(new SignatureHelpHandler(workspaceService))
              .AddHandler(new DocumentSymbolHandler(navigationService))
              .AddHandler(new HoverHandler(navigationService))
              .AddHandler(new FoldingRangeHandler(navigationService))
              .AddHandler(new SemanticTokensHandler(navigationService))
              .AddHandler(new ImplementationHandler(workspaceService))
              .AddHandler(new ReferenceHandler(navigationService))
              .AddHandler(new DefinitionHandler(navigationService))
              .AddHandler(new TypeDefinitionHandler(navigationService))
              .AddHandler(new TypeHierarchyHandler(navigationService))
              .AddHandler(new CodeActionHandler(workspaceService, codeAnalysisService))
              .AddHandler(new CompletionHandler(workspaceService, configurationService))
        // Workspace handlers
              .AddHandler(new DidChangeConfigurationHandler(configurationService))
              .AddHandler(new WorkspaceSymbolHandler(workspaceService))
        // Framework handlers
              .AddHandler(new SolutionDiagnosticsHandler(workspaceService, codeAnalysisService))
              .AddHandler(new ReloadWorkspaceHandler(workspaceService));

        server.OnInitialize(OnInitializeAsync);
        return server.Run();
    }
    private static async Task OnInitializeAsync(InitializeParams parameters, ServerInfo serverInfo) {
        ConfigureProcessObserver(parameters.ProcessId);
        ConfigureServerInfo(serverInfo);

        await configurationService.InitializeTask.ConfigureAwait(false);
        if (!workspaceService.InitializeWorkspace())
            LanguageServer.Proxy.ShowError(Resources.DotNetRegistrationFailed);

        workspaceService.WorkspaceStateChanged += (_, _) => navigationService.UpdateSolution(workspaceService.Solution);
        await workspaceService.LoadAsync(parameters.WorkspaceFolders, CancellationToken.None).ConfigureAwait(false);
        codeAnalysisService.StartWorkerThread();

        _ = LanguageServer.Server.SendNotification(Resources.LoadCompletedNotification, null);
        _ = LanguageServer.Proxy.RefreshWorkspaceTokens();
        _ = externalAccessService.StartListeningAsync(parameters.ProcessId, CancellationToken.None);
    }

    private static void ConfigureProcessObserver(int? pid) {
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
        serverInfo.Name = Assembly.GetExecutingAssembly().GetName().Name ?? string.Empty;
    }
    private static void ConfigureServices() {
        configurationService = new ConfigurationService();
        navigationService = new NavigationService();
        codeAnalysisService = new CodeAnalysisService(configurationService);
        workspaceService = new WorkspaceService(configurationService);
        externalAccessService = new ExternalAccessService(workspaceService);
    }
}
