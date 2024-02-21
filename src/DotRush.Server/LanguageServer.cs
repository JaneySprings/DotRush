using System.Diagnostics;
using DotRush.Server.Handlers;
using DotRush.Server.Services;
using Microsoft.Extensions.DependencyInjection;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Extensions.LanguageServer.Protocol.Server.WorkDone;
using OmniSharp.Extensions.LanguageServer.Server;
using OmniSharpLanguageServer = OmniSharp.Extensions.LanguageServer.Server.LanguageServer;

namespace DotRush.Server;

public class LanguageServer {
    public const string CodeAnalysisFeaturesAssembly = "Microsoft.CodeAnalysis.CSharp.Features";
    private static IServerWorkDoneManager? workDoneManager;

    public static async Task<IWorkDoneObserver> CreateWorkDoneObserverAsync() {
        return await workDoneManager!.Create(new WorkDoneProgressBegin());
    }

    public static async Task Main(string[] args) {
        var server = await OmniSharpLanguageServer.From(options => options
            .AddDefaultLoggingProvider()
            .WithInput(Console.OpenStandardInput())
            .WithOutput(Console.OpenStandardOutput())
            .WithServices(services => {
                services.AddSingleton<ConfigurationService>();
                services.AddSingleton<WorkspaceService>();
                services.AddSingleton<CodeActionService>();
                services.AddSingleton<CompilationService>();
                services.AddSingleton<DecompilationService>();
                services.AddSingleton<CommandsService>();
            })
            .WithHandler<DocumentSyncHandler>()
            .WithHandler<WatchedFilesHandler>()
            .WithHandler<WorkspaceFoldersHandler>()
            .WithHandler<WorkspaceSymbolsHandler>()
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
            .OnStarted((s, ct) => StartedHandlerAsync(s, args, ct))
        ).ConfigureAwait(false);

        await server.WaitForExit.ConfigureAwait(false);
    }

    private static async Task StartedHandlerAsync(ILanguageServer server, string[] targets, CancellationToken cancellationToken) {
        var clientSettings = server.Workspace.ClientSettings;
        var compilationService = server.Services.GetService<CompilationService>()!;
        var configurationService = server.Services.GetService<ConfigurationService>()!;
        var codeActionService = server.Services.GetService<CodeActionService>()!;
        var workspaceService = server.Services.GetService<WorkspaceService>()!;

        ObserveClientProcess(clientSettings.ProcessId);
        workDoneManager = server.WorkDoneManager;

        await configurationService.InitializeAsync(server.Configuration);    
        workspaceService.InitializeWorkspace();
        codeActionService.InitializeEmbeddedProviders();
        if (configurationService.EnableRoslynAnalyzers())
            compilationService.InitializeEmbeddedAnalyzers();

        workspaceService.AddProjectFiles(targets);
        workspaceService.StartSolutionLoading();
    }

    private static void ObserveClientProcess(long? pid) {
        if (pid == null || pid <= 0)
            return;

        var ideProcess = Process.GetProcessById((int)pid.Value);
        ideProcess.EnableRaisingEvents = true;
        ideProcess.Exited += (_, _) => Environment.Exit(0);
    }
}

// public static class Logger {
//     public static void Write(string message) {
//         var tempFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "log.txt");
//         File.AppendAllText(tempFile, message + Environment.NewLine);
//     }
// }