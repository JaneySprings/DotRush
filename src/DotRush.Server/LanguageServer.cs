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
    public static TextDocumentSelector SelectorForAllDocuments => TextDocumentSelector.ForLanguage("csharp", "xml", "xaml", "XAML");
    public static TextDocumentSelector SelectorForSourceCodeDocuments => TextDocumentSelector.ForLanguage("csharp");

    public static bool IsSourceCodeDocument(string filePath) {
        var allowedExtensions = new[] { ".cs", /* .fs .vb */};
        return allowedExtensions.Any(it => Path.GetExtension(filePath).Equals(it, StringComparison.OrdinalIgnoreCase));
    }
    public static bool IsAdditionalDocument(string filePath) {
        var allowedExtensions = new[] { ".xaml", /* maybe '.razor' ? */};
        return allowedExtensions.Any(it => Path.GetExtension(filePath).Equals(it, StringComparison.OrdinalIgnoreCase));
    }
    public static bool IsProjectFile(string filePath) {
        var allowedExtensions = new[] { ".csproj", /* fsproj vbproj */};
        return allowedExtensions.Any(it => Path.GetExtension(filePath).Equals(it, StringComparison.OrdinalIgnoreCase));
    }
    public static bool IsInternalCommandFile(string filePath) {
        return Path.GetFileName(filePath) == "resolve.drc";
    }

    private static IServerWorkDoneManager? workDoneManager;
    public static IWorkDoneObserver? CreateWorkDoneObserver() {
        var task = workDoneManager?.Create(new WorkDoneProgressBegin());
        return task?.Wait(TimeSpan.FromSeconds(2)) == true ? task.Result : null;
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
            .WithHandler<DidOpenTextDocumentHandler>()
            .WithHandler<DidChangeTextDocumentHandler>()
            .WithHandler<DidCloseTextDocumentHandler>()
            .WithHandler<WatchedFilesHandler>()
            .WithHandler<WorkspaceFoldersHandler>()
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

        await configurationService.InitializeAsync();    
        if (!workspaceService.TryInitializeWorkspace())
            return;

        codeActionService.InitializeEmbeddedProviders();
        if (configurationService.UseRoslynAnalyzers)
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