namespace DotRush.Debugging.Mono;

public class Program {
    private static void Main(string[] args) {
        Console.SetError(TextWriter.Null);
        Console.SetOut(TextWriter.Null);
        Console.SetIn(TextReader.Null);

        var debugSession = new DebugSession(Console.OpenStandardInput(), Console.OpenStandardOutput());
        debugSession.Start();
    }
}
