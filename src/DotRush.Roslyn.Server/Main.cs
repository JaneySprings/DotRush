using System.Reflection;
using DotRush.Common;
using DotRush.Common.Extensions;
using DotRush.Common.Logging;
using DotRush.Protocol;
using DotRush.Protocol.Models;
using DotRush.Roslyn.Server.Handlers.ExternalAccess;
using DotRush.Roslyn.Server.Handlers.TextDocument;
using DotRush.Roslyn.Server.Handlers.Workspace;
using DotRush.Roslyn.Server.Services;

namespace DotRush.Roslyn.Server;

public class Program {
    private static LanguageServer languageServer = null!;
    private static ConfigurationService configurationService = null!;
    private static WorkspaceService workspaceService = null!;
    private static CodeAnalysisService codeAnalysisService = null!;
    private static NavigationService navigationService = null!;
    private static TestExplorerService testExplorerService = null!;

    public static Task<int> Main(string[] args) {
        var input = Console.OpenStandardInput();
        var output = Console.OpenStandardOutput();
        Console.SetError(TextWriter.Null);
        Console.SetOut(TextWriter.Null);
        Console.SetIn(TextReader.Null);
        Localizer.Init();

        languageServer = new LanguageServer(input, output);
        ConfigureServerInfo();
        ConfigureServices();
        ConfigureHandlers();

        languageServer.OnInitialized(OnInitializedAsync);
        languageServer.OnUnhandledException(CurrentSessionLogger.Error);
        languageServer.OnShutdown(OnShutdownAsync);
        return languageServer.RunAsync();
    }
    private static Task OnInitializedAsync(InitializeParams parameters) {
        _ = SafeExtensions.InvokeAsync(async () => {
            await configurationService.InitializeTask;
            if (!workspaceService.InitializeWorkspace()) {
                CurrentSessionLogger.Error(Resources.DotNetRegistrationFailed);
                languageServer.Client.ShowMessage(MessageType.Error, Resources.DotNetRegistrationFailed);
            }

            try {
                await workspaceService.LoadAsync(parameters.WorkspaceFolders, CancellationToken.None);
            }
            finally {
                codeAnalysisService.StartWorkerThread();
            }

            languageServer.Client.SendNotification(Resources.LoadCompletedNotification);
            await languageServer.Client.RefreshSemanticTokensAsync(CancellationToken.None);
        });
        return Task.CompletedTask;
    }
    private static Task OnShutdownAsync() {
        workspaceService.Dispose();
        return Task.CompletedTask;
    }

    private static void ConfigureServerInfo() {
        var assemblyName = Assembly.GetExecutingAssembly().GetName();
        languageServer.ServerInfo = new ServerInfo {
            Name = assemblyName.Name ?? string.Empty,
            Version = assemblyName.Version?.ToString()
        };
    }
    private static void ConfigureServices() {
        configurationService = new ConfigurationService(languageServer);
        testExplorerService = new TestExplorerService();
        workspaceService = new WorkspaceService(configurationService, languageServer);
        navigationService = new NavigationService(workspaceService);
        codeAnalysisService = new CodeAnalysisService(configurationService, languageServer);
    }
    private static void ConfigureHandlers() {
        languageServer.AddHandler(new TextDocumentHandler(workspaceService, codeAnalysisService))
            .AddHandler(new DocumentFormattingHandler(workspaceService))
            .AddHandler(new RenameHandler(workspaceService))
            .AddHandler(new SignatureHelpHandler(workspaceService))
            .AddHandler(new DocumentSymbolHandler(navigationService))
            .AddHandler(new HoverHandler(navigationService))
            .AddHandler(new FoldingRangeHandler(navigationService))
            .AddHandler(new SemanticTokensHandler(navigationService))
            .AddHandler(new ImplementationHandler(navigationService))
            .AddHandler(new InlayHintHandler(workspaceService))
            .AddHandler(new ReferenceHandler(navigationService))
            .AddHandler(new DefinitionHandler(navigationService))
            .AddHandler(new TypeDefinitionHandler(navigationService))
            .AddHandler(new TypeHierarchyHandler(navigationService))
            .AddHandler(new CodeActionHandler(workspaceService, codeAnalysisService))
            .AddHandler(RuntimeInfo.IsRunningOnVSCode
                ? new CompletionV2Handler(workspaceService, configurationService)
                : new CompletionHandler(workspaceService, configurationService))
        // Workspace handlers
            .AddHandler(new DidChangeConfigurationHandler(configurationService))
            .AddHandler(new WorkspaceSymbolHandler(workspaceService))
        // Framework handlers
            .AddHandler(new WorkspaceDiagnosticsHandler(workspaceService, codeAnalysisService))
            .AddHandler(new ReloadWorkspaceHandler(workspaceService, navigationService, codeAnalysisService))
            .AddHandler(new TestExplorerHandler(testExplorerService, workspaceService));
    }
}
