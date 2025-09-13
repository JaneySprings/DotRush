using System.Diagnostics;
using System.Reflection;
using DotRush.Common.Logging;
using DotRush.Roslyn.Server.Extensions;
using DotRush.Roslyn.Server.Handlers.Framework;
using DotRush.Roslyn.Server.Handlers.TextDocument;
using DotRush.Roslyn.Server.Handlers.Workspace;
using DotRush.Roslyn.Server.Services;
using EmmyLua.LanguageServer.Framework.Protocol.Message.Initialize;
using EmmyLua.LanguageServer.Framework.Server;

namespace DotRush.Roslyn.Server;

public class Program {
    private static LanguageServer languageServer = null!;
    private static ConfigurationService configurationService = null!;
    private static WorkspaceService workspaceService = null!;
    private static CodeAnalysisService codeAnalysisService = null!;
    private static NavigationService navigationService = null!;
    private static TestExplorerService testExplorerService = null!;
    private static ExternalAccessService externalAccessService = null!;

    public static Task Main(string[] args) {
        Console.SetError(TextWriter.Null);
        Console.SetOut(TextWriter.Null);
        Console.SetIn(TextReader.Null);

        languageServer = LanguageServer.From(Console.OpenStandardInput(), Console.OpenStandardOutput());
        ConfigureServices();

        languageServer.AddHandler(new TextDocumentHandler(workspaceService, codeAnalysisService))
              .AddHandler(new DocumentFormattingHandler(workspaceService))
              .AddHandler(new RenameHandler(workspaceService))
              .AddHandler(new SignatureHelpHandler(workspaceService))
              .AddHandler(new DocumentSymbolHandler(navigationService))
              .AddHandler(new HoverHandler(navigationService))
              .AddHandler(new FoldingRangeHandler(navigationService))
              .AddHandler(new SemanticTokensHandler(navigationService))
              .AddHandler(new ImplementationHandler(workspaceService))
              .AddHandler(new InlayHintHandler(workspaceService))
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
              .AddHandler(new ReloadWorkspaceHandler(workspaceService))
              .AddHandler(new TestExplorerHandler(testExplorerService, workspaceService));

        languageServer.OnInitialize(OnInitializeAsync);
        return languageServer.Run();
    }
    private static async Task OnInitializeAsync(InitializeParams parameters, ServerInfo serverInfo) {
        ConfigureProcessObserver(parameters.ProcessId);
        ConfigureServerInfo(serverInfo);

        await configurationService.InitializeTask.ConfigureAwait(false);
        if (!workspaceService.InitializeWorkspace())
            languageServer.ShowError(Resources.DotNetRegistrationFailed);

        workspaceService.WorkspaceStateChanged += (_, _) => navigationService.UpdateSolution(workspaceService.Solution);
        await workspaceService.LoadAsync(parameters.WorkspaceFolders, CancellationToken.None).ConfigureAwait(false);
        codeAnalysisService.StartWorkerThread();

        _ = languageServer.SendNotification(Resources.LoadCompletedNotification, null);
        _ = languageServer.Client.RefreshWorkspaceTokens();
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
        configurationService = new ConfigurationService(languageServer);
        navigationService = new NavigationService();
        testExplorerService = new TestExplorerService();
        workspaceService = new WorkspaceService(configurationService, languageServer);
        codeAnalysisService = new CodeAnalysisService(configurationService, languageServer);
        externalAccessService = new ExternalAccessService(workspaceService);
    }
}
