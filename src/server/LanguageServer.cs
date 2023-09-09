using System.Diagnostics;
using DotRush.Server.Handlers;
using DotRush.Server.Logging;
using DotRush.Server.Services;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Extensions.LanguageServer.Protocol.Server.WorkDone;
using OmniSharp.Extensions.LanguageServer.Protocol.Window;
using OmniSharp.Extensions.LanguageServer.Server;
using OmniSharpLanguageServer = OmniSharp.Extensions.LanguageServer.Server.LanguageServer;

namespace DotRush.Server;

public class LanguageServer {
    public const string EmbeddedCodeFixAssembly = "Microsoft.CodeAnalysis.CSharp.Features";
    private static IServerWorkDoneManager? workDoneManager;

    public static async Task<IWorkDoneObserver> CreateWorkDoneObserverAsync() {
        return await workDoneManager!.Create(new WorkDoneProgressBegin());
    }

    public static async Task Main(string[] args) {
        var ideProcess = Process.GetProcessById(int.Parse(args[0]));
        ideProcess.EnableRaisingEvents = true;
        ideProcess.Exited += (s, e) => Environment.Exit(0);

        LogConfig.InitializeLog();

        var server = await OmniSharpLanguageServer.From(options => options
            .AddDefaultLoggingProvider()
            .WithInput(Console.OpenStandardInput())
            .WithOutput(Console.OpenStandardOutput())
            .WithServices(s => ConfigureServices(s))
            .WithHandler<DocumentSyncHandler>()
            .WithHandler<WatchedFilesHandler>()
            .WithHandler<WorkspaceFoldersHandler>()
            .WithHandler<HoverHandler>()
            // .WithHandler<SignatureHelpHandler>() WIP
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


    private static void ConfigureServices(IServiceCollection services) {
        services.AddSingleton<ConfigurationService>();
        services.AddSingleton<SolutionService>();
        services.AddSingleton<CodeActionService>();
        services.AddSingleton<CompilationService>();
        services.AddSingleton<DecompilationService>();
        //TODO: Temp
        LoggingService.Initialize();
    }

    private static async Task StartedHandlerAsync(ILanguageServer server, CancellationToken cancellationToken) {
        var compilationService = server.Services.GetService<CompilationService>();
        var codeActionService = server.Services.GetService<CodeActionService>();
        var decompilationService = server.Services.GetService<DecompilationService>();

        var configurationService = server.Services.GetService<ConfigurationService>();
        var solutionService = server.Services.GetService<SolutionService>();
        if (solutionService == null || configurationService == null)
            return;

        if (decompilationService?.DecompilationDirectory != null && Directory.Exists(decompilationService.DecompilationDirectory))
            Directory.Delete(decompilationService.DecompilationDirectory, true);

        var workspaceFolders = server.Workspace.ClientSettings.WorkspaceFolders?.Select(it => it.Uri.GetFileSystemPath());
        if (workspaceFolders == null) {
            server.Window.ShowWarning("No workspace folders found.");
            return;
        }

        workDoneManager = server.WorkDoneManager;
        var workDoneProgress = await CreateWorkDoneObserverAsync();

        configurationService.Initialize(server.Configuration);
        if (configurationService.IsRoslynAnalyzersEnabled())
            compilationService?.InitializeEmbeddedAnalyzers();

        solutionService.InitializeWorkspace();
        solutionService.AddWorkspaceFolders(workspaceFolders);
        solutionService.StartSolutionReloading(workDoneProgress);
    }
}