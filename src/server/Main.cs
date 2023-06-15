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
            .WithServices(s => ConfigureServices(s))
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


    private static void ConfigureServices(IServiceCollection services) {
        services.AddSingleton<SolutionService>();
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

        var workspaceFolders = server.Workspace.ClientSettings.WorkspaceFolders?.Select(it => it.Uri.GetFileSystemPath());
        if (workspaceFolders == null) {
            server.Window.ShowWarning("No workspace folders found.");
            return Task.CompletedTask;
        }

        solutionService.AddWorkspaceFolders(workspaceFolders);
        solutionService.ReloadSolution(path => {
            server.Window.ShowInfo($"Project {Path.GetFileNameWithoutExtension(path)} ready.");
        });

        return Task.CompletedTask;
    }
}