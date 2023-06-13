using System.Diagnostics;
using DotRush.Server.Handlers;
using DotRush.Server.Logging;
using DotRush.Server.Services;
using Microsoft.Extensions.DependencyInjection;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Extensions.LanguageServer.Protocol.Window;
using OmniSharp.Extensions.LanguageServer.Server;

namespace DotRush.Server;

public class Program {
    public static readonly string AnalyzersLocation = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "analyzers");

    public static async Task Main(string[] args) {
        var ideProcess = Process.GetProcessById(int.Parse(args[0]));
        ideProcess.EnableRaisingEvents = true;
        ideProcess.Exited += (s, e) => Environment.Exit(0);

        LogConfig.InitializeLog();
        
        var server = await LanguageServer.From(options => options
            .WithInput(Console.OpenStandardInput())
            .WithOutput(Console.OpenStandardOutput())
            .WithServices(s => ConfigureServices(s, args.Skip(1).ToArray()))
            .OnInitialized(InitializedHandler)
            .WithHandler<DocumentSyncHandler>()
            .WithHandler<WatchedFilesHandler>()
            .WithHandler<WorkspaceFoldersHandler>()
            .WithHandler<HoverHandler>()
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

        await server.WaitForExit.ConfigureAwait(false);
    }


    private static void ConfigureServices(IServiceCollection services, string[] targets) {
        services.AddSingleton<SolutionService>(x => new SolutionService(targets));
        services.AddSingleton<CodeActionService>();
        services.AddSingleton<CompilationService>();

        //TODO: Temp
        LoggingService.Initialize();
    }

    private static Task InitializedHandler(ILanguageServer server, InitializeParams request, InitializeResult response, CancellationToken cancellationToken) {
        var compilationService = server.Services.GetService<CompilationService>();
        var solutionService = server.Services.GetService<SolutionService>();
        if (compilationService == null || solutionService == null) 
            return Task.CompletedTask;

        solutionService.ReloadSolution(path => {
            server.Window.ShowMessage(new ShowMessageParams {
                Message = $"Project {Path.GetFileNameWithoutExtension(path)} ready.",
                Type = MessageType.Log
            });
        });

        return Task.CompletedTask;
    }
}