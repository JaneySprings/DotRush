using System.Text.Json;

namespace DotRush.Essentials.TestExplorer;

public class Program {
    public static readonly Dictionary<string, Action<string[]>> CommandHandler = new() {
        { "--list-tests", DiscoverTests },
        { "--convert", ConvertReport },
        { "--run", RunTestHost }
    };

    private static void Main(string[] args) {
        if (args.Length == 0)
            return;
        if (CommandHandler.TryGetValue(args[0], out var command))
            command.Invoke(args);
    }

    public static void DiscoverTests(string[] args) {
        var tests = TestExplorer.DiscoverTests(args[1]);
        Console.WriteLine(JsonSerializer.Serialize(tests));
    }
    public static void ConvertReport(string[] args) {
        var results = ReportConverter.ReadReport(args[1]);
        Console.WriteLine(JsonSerializer.Serialize(results));
    }
    public static void RunTestHost(string[] args) {
        var result = TestHost.RunForDebugAsync(args[1], args[2]).Result;
        Console.WriteLine(JsonSerializer.Serialize(result));
    }
}
