using System.Diagnostics;
using DotRush.Roslyn.Common.Logging;
using DotRush.Roslyn.Server.Extensions;
using DotRush.Roslyn.Server.Handlers.TextDocument;
using DotRush.Roslyn.Server.Handlers.Workspace;
using DotRush.Roslyn.Server.Services;
using Microsoft.Extensions.DependencyInjection;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Extensions.LanguageServer.Server;
using OmniSharpLanguageServer = OmniSharp.Extensions.LanguageServer.Server.LanguageServer;

namespace DotRush.Roslyn.Server;

public class LanguageServer {
    public const string CodeAnalysisFeaturesAssembly = "Microsoft.CodeAnalysis.CSharp.Features";
    public static TextDocumentSelector SelectorForAllDocuments => TextDocumentSelector.ForPattern("**/*.cs", "**/*.xaml");
    public static TextDocumentSelector SelectorForSourceCodeDocuments => TextDocumentSelector.ForPattern("**/*.cs");

    public static async Task Main(string[] args) {
        var server = await OmniSharpLanguageServer.From(options => options
            .AddDefaultLoggingProvider()
            .WithInput(Console.OpenStandardInput())
            .WithOutput(Console.OpenStandardOutput())
            .WithServices(services => services
                .AddSingleton<ConfigurationService>()
                .AddSingleton<WorkspaceService>()
                .AddSingleton<CodeAnalysisService>()
                .AddSingleton<NavigationService>()
                .AddSingleton<ExternalAccessService>())
            .WithHandler<DidOpenTextDocumentHandler>()
            .WithHandler<DidChangeTextDocumentHandler>()
            .WithHandler<DidCloseTextDocumentHandler>()
            .WithHandler<DidChangeWatchedFilesHandler>()
            .WithHandler<DidChangeWorkspaceFoldersHandler>()
            .WithHandler<WorkspaceSymbolsHandler>()
            .WithHandler<DocumentSymbolHandler>()
            .WithHandler<HoverHandler>()
            .WithHandler<FoldingRangeHandler>()
            .WithHandler<SignatureHelpHandler>()
            .WithHandler<FormattingHandler>()
            .WithHandler<RangeFormattingHandler>()
            .WithHandler<RenameHandler>()
            .WithHandler<CompletionHandler>()
            .WithHandler<CodeActionHandler>()
            .WithHandler<ReferencesHandler>()
            .WithHandler<ImplementationHandler>()
            .WithHandler<DefinitionHandler>()
            .WithHandler<TypeDefinitionHandler>()
            .OnStarted(StartedHandlerAsync)
        ).ConfigureAwait(false);

        await server.WaitForExit.ConfigureAwait(false);
    }
    private static async Task StartedHandlerAsync(ILanguageServer server, CancellationToken cancellationToken) {
        var clientSettings = server.Workspace.ClientSettings;
        ObserveClientProcess(clientSettings.ProcessId);
        
        var configurationService = server.Services.GetService<ConfigurationService>()!;
        var navigationService = server.Services.GetService<NavigationService>()!;
        var workspaceService = server.Services.GetService<WorkspaceService>()!;
        var externalAccessService = server.Services.GetService<ExternalAccessService>()!;

        await configurationService.InitializeAsync().ConfigureAwait(false);
        if (!workspaceService.InitializeWorkspace())
            server.ShowError(Resources.DotNetRegistrationFailed);

        workspaceService.WorkspaceStateChanged += (_, _) => navigationService.UpdateSolution(workspaceService.Solution);
        if (configurationService.ProjectOrSolutionFiles.Count != 0) {
            _ = workspaceService.LoadAsync(configurationService.ProjectOrSolutionFiles, CancellationToken.None);
            _ = externalAccessService.StartListeningAsync(CancellationToken.None);
        }
    }

    private static void ObserveClientProcess(long? pid) {
        if (pid == null || pid <= 0)
            return;

        var ideProcess = Process.GetProcessById((int)pid.Value);
        ideProcess.EnableRaisingEvents = true;
        ideProcess.Exited += (_, _) => {
            CurrentSessionLogger.Debug($"Shutting down server because client process has exited");
            Environment.Exit(0);
        };
        CurrentSessionLogger.Debug($"Server is observing client process {ideProcess.ProcessName} (PID: {pid})");
    }
}
