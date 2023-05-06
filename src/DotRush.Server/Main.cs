using System.Diagnostics;
using DotRush.Server.Handlers;
using DotRush.Server.Logging;
using DotRush.Server.Services;
using Microsoft.Extensions.DependencyInjection;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Extensions.LanguageServer.Server;

namespace DotRush.Server;

public class Program {
    private static LanguageServer? Server;
    public static async Task Main(string[] args) {
        var ideProcess = Process.GetProcessById(int.Parse(args[0]));
        ideProcess.EnableRaisingEvents = true;
        ideProcess.Exited += (s, e) => Environment.Exit(0);

        LogConfig.InitializeLog();
        
        Server = await LanguageServer.From(options => options
            .WithInput(Console.OpenStandardInput())
            .WithOutput(Console.OpenStandardOutput())
            .WithServices(s => ConfigureServices(s, args.Skip(1).ToArray()))
            .OnInitialize(InitializeHandler)
            .OnNotification<FrameworkChangedParams>("frameworkChanged", FrameworkChanged)
            .OnNotification<ReloadTargetsParams>("reloadTargets", ReloadTargets)
            .WithHandler<DocumentSyncHandler>()
            .WithHandler<WatchedFilesHandler>()
            .WithHandler<WorkspaceFoldersHandler>()
            .WithHandler<FormattingHandler>()
            .WithHandler<RangeFormattingHandler>()
            .WithHandler<RenameHandler>()
            .WithHandler<CompletionHandler>()
            .WithHandler<CodeActionHandler>()
            .WithHandler<ReferencesHandler>()
            .WithHandler<ImplementationHandler>()
            .WithHandler<DefinitionHandler>()
            .WithHandler<TypeDefinitionHandler>()
        ).ConfigureAwait(false);

        await Server.WaitForExit.ConfigureAwait(false);
    }


    private static void ConfigureServices(IServiceCollection services, string[] targets) {
        services.AddSingleton<SolutionService>(x => new SolutionService(targets));
        services.AddSingleton<CodeActionService>();
        services.AddSingleton<CompilationService>();

        //TODO: Temp
        LoggingService.Initialize();
    }

    private static Task InitializeHandler(ILanguageServer server, InitializeParams request, CancellationToken cancellationToken) {
        var solutionService = server.Services.GetService<SolutionService>();
        var compilationService = server.Services.GetService<CompilationService>();
        if (solutionService == null || compilationService == null) 
            return Task.CompletedTask;

        solutionService.ProjectLoaded = path => {
            compilationService.Compile(path, server.TextDocument);
        };

        return Task.CompletedTask;
    }

    private static void FrameworkChanged(FrameworkChangedParams parameters, CancellationToken cancellationToken) {
        var solutionService = Server?.Services.GetService<SolutionService>();
        if (solutionService == null) 
            return;

        solutionService.TargetFramework = parameters.framework;
        solutionService.ForceReload(cancellationToken);
    }

    private static void ReloadTargets(ReloadTargetsParams parameters, CancellationToken cancellationToken) {
        var solutionService = Server?.Services.GetService<SolutionService>();
        if (solutionService == null) 
            return;

        solutionService.ForceReload(cancellationToken);
    }
}