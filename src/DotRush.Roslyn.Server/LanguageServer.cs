using System.Diagnostics;
using DotRush.Server.Extensions;
using DotRush.Server.Handlers;
using DotRush.Server.Logging;
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
    public static TextDocumentSelector SelectorForAllDocuments => TextDocumentSelector.ForPattern("**/*.cs", "**/*.xaml");
    public static TextDocumentSelector SelectorForSourceCodeDocuments => TextDocumentSelector.ForPattern("**/*.cs");

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
    public static async Task<IWorkDoneObserver?> CreateWorkDoneObserverAsync() {
        return workDoneManager == null ? null : await workDoneManager.Create(new WorkDoneProgressBegin());
    }

    public static async Task Main(string[] args) {
        SessionLogger.LogDebug($"Server created with targets: {string.Join(", ", args)}");
        var server = await OmniSharpLanguageServer.From(options => options
            .AddDefaultLoggingProvider()
            .WithInput(Console.OpenStandardInput())
            .WithOutput(Console.OpenStandardOutput())
            .WithServices(services => services
                .AddSingleton<ConfigurationService>()
                .AddSingleton<WorkspaceService>()
                .AddSingleton<CodeActionService>()
                .AddSingleton<CompilationService>()
                .AddSingleton<DecompilationService>()
                .AddSingleton<CommandsService>())
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
            .OnStarted((s, _) => StartedHandlerAsync(s, args))
        ).ConfigureAwait(false);

        await server.WaitForExit.ConfigureAwait(false);
    }
    private static async Task StartedHandlerAsync(ILanguageServer server, IEnumerable<string> targets) {
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

        if (!targets.Any()) {
            var workspaceFolders = server.ClientSettings.WorkspaceFolders?.Select(it => it.Uri.GetFileSystemPath());
            targets = WorkspaceExtensions.GetProjectFiles(workspaceFolders);
            SessionLogger.LogDebug($"No targets provided, used auto-detected targets: {string.Join(' ', targets)}");
        }

        workspaceService.AddProjectFiles(targets);
        _ = workspaceService.LoadSolutionAsync();
    }
    private static void ObserveClientProcess(long? pid) {
        if (pid == null || pid <= 0)
            return;

        var ideProcess = Process.GetProcessById((int)pid.Value);
        ideProcess.EnableRaisingEvents = true;
        ideProcess.Exited += (_, _) => {
            SessionLogger.LogDebug($"Shutting down server because client process [{ideProcess.ProcessName}] has exited");
            Environment.Exit(0);
        };
        SessionLogger.LogDebug($"Server is observing client process {ideProcess.ProcessName} (PID: {pid})");
    }
}
