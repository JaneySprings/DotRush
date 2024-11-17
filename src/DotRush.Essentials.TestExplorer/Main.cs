using System.Text.Json;

namespace DotRush.Essentials.TestExplorer;

public class Program {
    public static readonly Dictionary<string, Action<string[]>> CommandHandler = new() {
        { "--list-tests", DiscoverTests }
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
}
