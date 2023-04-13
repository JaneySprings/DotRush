using System.Diagnostics;
using DotRush.Server.Logging;
using DotRush.Server.Services;

namespace DotRush.Server;

public class Program {
    public static async Task Main(string[] args) {
        LogConfig.InitializeLog();
        ConfigureServices(args.Skip(1).ToArray());

        var server = new ServerSession(Console.OpenStandardInput(), Console.OpenStandardOutput());
        var ideProcess = Process.GetProcessById(int.Parse(args[0]));

        ideProcess.EnableRaisingEvents = true;
        ideProcess.Exited += (sender, e) => Environment.Exit(0);
        
        await server.Listen();
    }

    private static void ConfigureServices(string[] targets) {
        CompilationService.Initialize();
        RefactoringService.Initialize();
        CompletionService.Initialize();
        DocumentService.Initialize();
        LoggingService.Initialize();

        SolutionService.Initialize(targets);
    }
}